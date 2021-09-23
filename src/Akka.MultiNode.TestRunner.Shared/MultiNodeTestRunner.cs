//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.IO;
using Akka.MultiNode.Shared;
using Akka.MultiNode.Shared.Environment;
using Akka.MultiNode.Shared.Persistence;
using Akka.MultiNode.Shared.Sinks;
using Akka.MultiNode.Shared.TrxReporter;
using Akka.MultiNode.TestRunner.Shared.Helpers;
using Akka.Remote.TestKit;
using Akka.Util;
using Xunit;
using ErrorMessage = Xunit.Sdk.ErrorMessage;

#if CORECLR
using System.Runtime.Loader;
#endif

namespace Akka.MultiNode.TestRunner.Shared
{
    /// <summary>
    /// Entry point for the MultiNodeTestRunner
    /// </summary>
    public class MultiNodeTestRunner : IDisposable
    {
        private readonly string _platformName = PlatformDetector.Current == PlatformDetector.PlatformType.NetCore ? "netcore" : "net";

        private string _currentAssembly;
        private IActorRef _tcpLogger;
        
        protected ActorSystem TestRunSystem;
        protected IActorRef SinkCoordinator;

        public event Action<MultiNodeTest> TestStarted;
        public event Action<MultiNodeTestResult> TestPassed;
        public event Action<MultiNodeTestResult> TestFailed;
        public event Action<MultiNodeTest, string> TestSkipped;
        public event Action<MultiNodeTest, Exception> Exception;

        private void Initialize(string assemblyPath, MultiNodeTestRunnerOptions options)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));

            var extension = Path.GetExtension(assemblyPath).ToLowerInvariant();
            if (extension != ".exe" && extension != ".dll")
                throw new ArgumentException($"Invalid assembly type, expected .exe or .dll, found: {extension}");

            var fileName = Path.GetFileName(assemblyPath);
            if (!string.IsNullOrEmpty(_currentAssembly))
            {
                if(!_currentAssembly.Equals(fileName))
                {
                    throw new ArgumentException(
                        $"Runner can only run a single assembly at a time, currently running: {_currentAssembly}, " +
                        $"requested assembly: {fileName}");
                }

                return;
            }
            
            _currentAssembly = fileName;
            // Perform output cleanup before anything is logged
            if (options.ClearOutputDirectory && Directory.Exists(options.OutputDirectory))
                Directory.Delete(options.OutputDirectory, true);

            TestRunSystem = ActorSystem.Create("TestRunnerLogging");

            var suiteName = Path.GetFileNameWithoutExtension(Path.GetFullPath(assemblyPath));
            SinkCoordinator = CreateSinkCoordinator(options, suiteName);

            _tcpLogger = TestRunSystem.ActorOf(Props.Create(() => new TcpLoggingServer(SinkCoordinator)), "TcpLogger");
            var listenEndpoint = new IPEndPoint(IPAddress.Parse(options.ListenAddress), options.ListenPort);
            TestRunSystem.Tcp().Tell(new Tcp.Bind(_tcpLogger, listenEndpoint), sender: _tcpLogger);

            EnableAllSinks(assemblyPath, options);

            // Set MNTR environment for correct tests discovery
            MultiNodeEnvironment.Initialize();

            // In NetCore, if the assembly file hasn't been touched, 
            // XunitFrontController would fail loading external assemblies and its dependencies.
            PreLoadTestAssembly_WhenNetCore(assemblyPath);
        }
        
        public MultiNodeTestResult ExecuteSpec(MultiNodeTest test, MultiNodeTestRunnerOptions options)
        {
            Initialize(test.AssemblyPath, options);
            
            // If port was set random, request the actual port from TcpLoggingServer
            var listenPort = options.ListenPort > 0 
                ? options.ListenPort 
                : _tcpLogger.Ask<int>(TcpLoggingServer.GetBoundPort.Instance).Result;

            TestStarted?.Invoke(test);
            try
            {
                if (options.SpecNames != null &&
                    options.SpecNames.All(name => test.TestName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) < 0))
                {
                    test.SkipReason = "Excluded by filtering";
                }
                
                if (!string.IsNullOrEmpty(test.SkipReason))
                {
                    PublishRunnerMessage($"Skipping [{test.MethodName}]. Reason: [{test.SkipReason}]");
                    TestSkipped?.Invoke(test, test.SkipReason);
                    return null;
                }
                
                // touch test.Nodes to load details
                var nodes = test.Nodes;

                var result = RunSpec(options, test, listenPort);
                if(result.Status == MultiNodeTestResult.TestStatus.Failed)
                    TestFailed?.Invoke(result);
                else
                    TestPassed?.Invoke(result);
                return result;
            }
            catch (Exception e)
            {
                Exception?.Invoke(test, e);
                PublishRunnerMessage(e.Message);
                return null;
            }
        }
        
        /// <summary>
        /// Executes multi-node tests from given assembly
        /// </summary>
        public List<MultiNodeTestResult> ExecuteAssembly(string assemblyPath, MultiNodeTestRunnerOptions options)
        {
            Initialize(assemblyPath, options);
            
            // Here is where main action goes
            var results = DiscoverAndRunSpecs(assemblyPath, options);

            return results;
        }
        
        public void Dispose()
        {
            AbortTcpLoggingServer();
            CloseAllSinks();

            // Block until all Sinks have been terminated.
            TestRunSystem?.WhenTerminated.Wait(TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Discovers all tests in given assembly
        /// </summary>
        public static (List<MultiNodeTest> Tests, List<ErrorMessage> Errors) DiscoverSpecs(string assemblyPath)
        {
            MultiNodeEnvironment.Initialize();

            using (var controller = new XunitFrontController(AppDomainSupport.IfAvailable, assemblyPath))
            {
                using (var discovery = new Discovery(assemblyPath))
                {
                    controller.Find(false, discovery, TestFrameworkOptions.ForDiscovery());
                    discovery.Finished.WaitOne();
                    return (discovery.Tests, discovery.Errors);
                }
            }
        }

        private List<MultiNodeTestResult> DiscoverAndRunSpecs(string assemblyPath, MultiNodeTestRunnerOptions options)
        {
            var testResults = new List<MultiNodeTestResult>();
            PublishRunnerMessage($"Running MultiNodeTests for {assemblyPath}");

            var (discoveredTests, errors) = DiscoverSpecs(assemblyPath);
            if (errors.Any())
            {
                ReportDiscoveryErrors(errors);
            }

            // If port was set random, request the actual port from TcpLoggingServer
            var listenPort = options.ListenPort > 0 
                ? options.ListenPort 
                : _tcpLogger.Ask<int>(TcpLoggingServer.GetBoundPort.Instance).Result;
            
            foreach (var test in discoveredTests)
            {
                TestStarted?.Invoke(test);
                try
                {
                    if (options.SpecNames != null &&
                        options.SpecNames.All(name => test.TestName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) < 0))
                    {
                        test.SkipReason = "Excluded by filtering";
                    }
                
                    if (!string.IsNullOrEmpty(test.SkipReason))
                    {
                        PublishRunnerMessage($"Skipping [{test.MethodName}]. Reason: [{test.SkipReason}]");
                        testResults.Add(new MultiNodeTestResult(test));
                        TestSkipped?.Invoke(test, test.SkipReason);
                        continue;
                    }
                
                    // touch test.Nodes to load details
                    var nodes = test.Nodes;

                    // Run test on several nodes and report results
                    var result = RunSpec(options, test, listenPort);
                    if(result.Status == MultiNodeTestResult.TestStatus.Failed)
                        TestFailed?.Invoke(result);
                    else
                        TestPassed?.Invoke(result);
                
                    testResults.Add(result);
                }
                catch (Exception e)
                {
                    Exception?.Invoke(test, e);
                    PublishRunnerMessage(e.Message);
                }
            }

            return testResults;
        }

        private MultiNodeTestResult RunSpec(MultiNodeTestRunnerOptions options, MultiNodeTest test, int listenPort)
        {
            PublishRunnerMessage($"Starting test {test.MethodName}");
            StartNewSpec(test);

            var timelineCollector = TestRunSystem.ActorOf(Props.Create(() => new TimelineLogCollectorActor()));
            //TODO: might need to do some validation here to avoid the 260 character max path error on Windows
            var folder = Directory.CreateDirectory(Path.Combine(options.OutputDirectory, test.TestName));
            var testOutputDir = folder.FullName;

            var testResult = new MultiNodeTestResult(test);
            
            var nodeProcesses = new List<(NodeTest, Process)>();
            foreach (var nodeTest in test.Nodes)
            {
                //Loop through each test, work out number of nodes to run on and kick off process
                var sbArguments = new StringBuilder()
                    .Append($@"-Dmultinode.test-class=""{nodeTest.Test.TypeName}"" ")
                    .Append($@"-Dmultinode.test-method=""{nodeTest.Test.MethodName}"" ")
                    .Append($@"-Dmultinode.max-nodes={test.Nodes.Count} ")
                    .Append($@"-Dmultinode.server-host=""{"localhost"}"" ")
                    .Append($@"-Dmultinode.host=""{"localhost"}"" ")
                    .Append($@"-Dmultinode.index={nodeTest.Node - 1} ")
                    .Append($@"-Dmultinode.role=""{nodeTest.Role}"" ")
                    .Append($@"-Dmultinode.listen-address={options.ListenAddress} ")
                    .Append($@"-Dmultinode.listen-port={listenPort} ")
                    .Append($@"-Dmultinode.test-assembly=""{test.AssemblyPath}"" ");

                // Configure process for node
                var process = BuildNodeProcess(test.AssemblyPath, sbArguments);
                nodeProcesses.Add((nodeTest, process));

                // Start process for node
                StartNodeProcess(process, nodeTest, options, folder, timelineCollector, testResult);
            }

            // Wait for all nodes to finish and collect results
            WaitForNodeExit(testResult, nodeProcesses);

            PublishRunnerMessage("Waiting 3 seconds for all messages from all processes to be collected.");
            Thread.Sleep(TimeSpan.FromSeconds(3));

            // Save timelined logs to file system
            DumpAggregatedSpecLogs(testResult, options, testOutputDir, timelineCollector);
            
            FinishSpec(test, timelineCollector);
            
            return testResult;
        }

        private void DumpAggregatedSpecLogs(
            MultiNodeTestResult result,
            MultiNodeTestRunnerOptions options, 
            string testOutputDir,
            IActorRef timelineCollector)
        {
            if (testOutputDir == null) return;

            var dumpPath = Path.GetFullPath(Path.Combine(testOutputDir, "aggregated.txt"));
            result.Attachments.Add(new MultiNodeTestResult.Attachment{Title = "Aggregated", Path = dumpPath});
            string consoleLog = null;
            var dumpTasks = new List<Task>()
            {
                // Dump aggregated timeline to file for this test
                timelineCollector.Ask<Done>(new TimelineLogCollectorActor.DumpToFile(dumpPath)),
                // Print aggregated timeline into the console
                timelineCollector.Ask<string>(new TimelineLogCollectorActor.PrintToConsole()).ContinueWith(t =>
                {
                    consoleLog = t.Result;
                })
            };

            if (result.Status == MultiNodeTestResult.TestStatus.Failed)
            {
                var failedSpecPath = Path.GetFullPath(Path.Combine(options.OutputDirectory, options.FailedSpecsDirectory, $"{result.Test.TestName}.txt"));
                var dumpFailureArtifactTask = timelineCollector.Ask<Done>(new TimelineLogCollectorActor.DumpToFile(failedSpecPath));
                dumpTasks.Add(dumpFailureArtifactTask);
                result.Attachments.Add(new MultiNodeTestResult.Attachment{Title = "Fail log", Path = failedSpecPath});
            }

            Task.WaitAll(dumpTasks.ToArray());
            result.ConsoleOutput = consoleLog;
        }

        private void WaitForNodeExit(MultiNodeTestResult result, List<(NodeTest, Process)> nodeProcesses)
        {
            try
            {
                for (var i = 0; i < nodeProcesses.Count; i++)
                {
                    var (test, process) = nodeProcesses[i];
                    process.WaitForExit();
                    Console.WriteLine($"Process for test {test.Name} finished with code {process.ExitCode}");
                    var nodeResult = result.NodeResults.First(n => n.Index == test.Node); 
                    nodeResult.Result = process.ExitCode == 0
                        ? MultiNodeTestResult.TestStatus.Passed
                        : MultiNodeTestResult.TestStatus.Failed;
                }
            }
            finally
            {
                foreach (var (_, process) in nodeProcesses)
                {
                    process.Dispose();
                }
            }
        }
        
        private Process BuildNodeProcess(string assemblyPath, StringBuilder sbArguments)
        {
            var nodeRunnerReferencedAssembly = typeof(NodeRunner.Nothing).Assembly;
            var nodeRunnerAssemblyName = nodeRunnerReferencedAssembly.GetName().Name;

            var assembly = Assembly.LoadFrom(assemblyPath);
            var attribute = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            
            var nodeRunnerFileName = nodeRunnerAssemblyName + (attribute.FrameworkName.StartsWith(".NETFramework") ? ".exe" : ".dll");
            
            var searchPaths = new []
            {
                AppContext.BaseDirectory,
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            };
            
            // The most robust way is to just scan possible locations and find runner assembly
            var nodeRunnerPath = FileToolInPaths(nodeRunnerFileName, dirPaths: searchPaths);
            if (!nodeRunnerPath.HasValue)
                throw new Exception($"Failed to find node runner '{nodeRunnerFileName}' at paths: {string.Join(", ", searchPaths)}");

            var nodeRunnerDir = Path.GetDirectoryName(assemblyPath);
            if (PlatformDetector.IsNetCore)
            {
                sbArguments.Insert(0, $"{nodeRunnerPath.Value} ");

                // Under .net core "*.runtimeconfig.json" is required to run NodeRunner as a console app
                CreateRuntimeConfigIfNotExists(nodeRunnerReferencedAssembly, nodeRunnerDir);
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = PlatformDetector.IsNetCore ? "dotnet" : nodeRunnerPath.Value,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Arguments = sbArguments.ToString(),
                    WorkingDirectory = PlatformDetector.IsNetCore ? nodeRunnerDir : null
                }
            };
            
            return process;
        }

        private static void CreateRuntimeConfigIfNotExists(Assembly nodeRunnerReferencedAssembly, string nodeRunnerDir)
        {
            var runtimeConfigContent = RuntimeConfigGenerator.GetRuntimeConfigContent(nodeRunnerReferencedAssembly);
            var runtimeConfigPath = Path.Combine(nodeRunnerDir, nodeRunnerReferencedAssembly.GetName().Name + ".runtimeconfig.json");
            if (!File.Exists(runtimeConfigPath))
                File.WriteAllText(runtimeConfigPath, runtimeConfigContent);
        }

        private void StartNodeProcess(
            Process process, 
            NodeTest nodeTest,
            MultiNodeTestRunnerOptions options, 
            DirectoryInfo specFolder,
            IActorRef timelineCollector,
            MultiNodeTestResult result)
        {
            var closureTest = nodeTest;
            var nodeIndex = nodeTest.Node;
            var nodeRole = nodeTest.Role;
            var logFilePath = Path.GetFullPath(Path.Combine(specFolder.FullName, $"node{nodeIndex}__{nodeRole}__{_platformName}.txt"));
            var nodeInfo = new TimelineLogCollectorActor.NodeInfo(nodeIndex, nodeRole, _platformName, nodeTest.Test.TestName);
            var fileActor = TestRunSystem.ActorOf(Props.Create(() => new FileSystemAppenderActor(logFilePath)));
            result.Attachments.Add(new MultiNodeTestResult.Attachment{Title = $"Node {nodeIndex} [{nodeRole}]", Path = logFilePath});
            process.OutputDataReceived += (sender, eventArgs) =>
            {
                if (eventArgs?.Data != null)
                {
                    fileActor.Tell(eventArgs.Data);
                    timelineCollector.Tell(new TimelineLogCollectorActor.LogMessage(nodeInfo, eventArgs.Data));
                    if (options.TeamCityFormattingOn)
                    {
                        // teamCityTest.WriteStdOutput(eventArgs.Data); TODO: open flood gates
                    }
                }
            };
            process.Exited += (sender, eventArgs) =>
            {
                if (process.ExitCode == 0)
                {
                    ReportSpecPassFromExitCode(nodeIndex, nodeRole, closureTest.Test.TestName);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            PublishRunnerMessage($"Started node {nodeIndex} : {nodeRole} on pid {process.Id}");
        }

        private void ReportDiscoveryErrors(List<ErrorMessage> errors)
        {
            var sb = new StringBuilder();
            sb.AppendLine("One or more exception was thrown while discovering test cases. Test Aborted.");
            foreach (var err in errors)
            {
                for (int i = 0; i < err.ExceptionTypes.Length; ++i)
                {
                    sb.AppendLine();
                    sb.Append($"{err.ExceptionTypes[i]}: {err.Messages[i]}");
                    sb.Append(err.StackTraces[i]);
                }
            }

            PublishRunnerMessage(sb.ToString());
            Console.Out.WriteLine(sb.ToString());
        }

        private void PreLoadTestAssembly_WhenNetCore(string assemblyPath)
        {
#if CORECLR
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            var asms = assembly.GetReferencedAssemblies();
            var basePath = Path.GetDirectoryName(assemblyPath);
            foreach (var asm in asms)
            {
                try
                {
                    Assembly.Load(new AssemblyName(asm.FullName));
                }
                catch (Exception)
                {
                    var path = Path.Combine(basePath, asm.Name + ".dll");
                    try
                    {
                        AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                    }
                    catch (Exception e)
                    {
                        Console.Out.WriteLine($"Failed to load dll [{path}]: {e}");
                    }
                }
            }
#endif
        }

        private IActorRef CreateSinkCoordinator(MultiNodeTestRunnerOptions options, string suiteName)
        {
            Props coordinatorProps;
            switch (options.Reporter.ToLowerInvariant())
            {
                case "trx":
                    coordinatorProps = Props.Create(() => new SinkCoordinator(new[] {new TrxMessageSink(suiteName)}));
                    break;

                case "teamcity":
                    coordinatorProps = Props.Create(() =>
                        new SinkCoordinator(new[] {new TeamCityMessageSink(Console.WriteLine, suiteName)}));
                    break;

                case "console":
                    coordinatorProps = Props.Create(() => new SinkCoordinator(new[] {new ConsoleMessageSink()}));
                    break;

                default:
                    throw new ArgumentException(
                        $"Given reporter name '{options.Reporter}' is not understood, valid reporters are: trx, teamcity, and console");
            }

            return TestRunSystem.ActorOf(coordinatorProps, "sinkCoordinator");
        }

        static string ChangeDllPathPlatform(string path, string targetPlatform)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), "..", targetPlatform, Path.GetFileName(path)));
        }

        private void EnableAllSinks(string assemblyName, MultiNodeTestRunnerOptions options)
        {
            var now = DateTime.UtcNow;

            // if multinode.output-directory wasn't specified, the results files will be written
            // to the same directory as the test assembly.
            var outputDirectory = options.OutputDirectory;

            MessageSink CreateJsonFileSink()
            {
                var fileName = FileNameGenerator.GenerateFileName(outputDirectory, assemblyName, _platformName, ".json", now);
                var jsonStoreProps = Props.Create(() => new FileSystemMessageSinkActor(new JsonPersistentTestRunStore(), fileName, !options.TeamCityFormattingOn, true));
                return new FileSystemMessageSink(jsonStoreProps);
            }

            MessageSink CreateVisualizerFileSink()
            {
                var fileName = FileNameGenerator.GenerateFileName(outputDirectory, assemblyName, _platformName, ".html", now);
                var visualizerProps = Props.Create(() => new FileSystemMessageSinkActor(new VisualizerPersistentTestRunStore(), fileName, !options.TeamCityFormattingOn, true));
                return new FileSystemMessageSink(visualizerProps);
            }

            var fileSystemSink = CommandLine.GetProperty("multinode.enable-filesink");
            if (!string.IsNullOrEmpty(fileSystemSink))
            {
                SinkCoordinator.Tell(new SinkCoordinator.EnableSink(CreateJsonFileSink()));
                SinkCoordinator.Tell(new SinkCoordinator.EnableSink(CreateVisualizerFileSink()));
            }
        }

        private void AbortTcpLoggingServer()
        {
            _tcpLogger?.Ask<TcpLoggingServer.ListenerStopped>(new TcpLoggingServer.StopListener(), TimeSpan.FromMinutes(1)).Wait();
        }

        private void CloseAllSinks()
        {
            SinkCoordinator?.Tell(new SinkCoordinator.CloseAllSinks());
        }

        private void StartNewSpec(MultiNodeTest test)
        {
            SinkCoordinator.Tell(test);
        }

        private void ReportSpecPassFromExitCode(int nodeIndex, string nodeRole, string testName)
        {
            SinkCoordinator.Tell(new NodeCompletedSpecWithSuccess(nodeIndex, nodeRole, testName + " passed."));
        }

        private void FinishSpec(MultiNodeTest test, IActorRef timelineCollector)
        {
            var log = timelineCollector.Ask<SpecLog>(new TimelineLogCollectorActor.GetSpecLog(), TimeSpan.FromMinutes(1)).Result;
            SinkCoordinator.Tell(new EndSpec(test, log));
        }

        private void PublishRunnerMessage(string message)
        {
            SinkCoordinator.Tell(new SinkCoordinator.RunnerMessage(message));
        }

        /// <summary>
        /// Finds given tool in specified paths
        /// </summary>
        private Option<string> FileToolInPaths(string toolFileName, params string[] dirPaths)
        {
            foreach (var dir in dirPaths)
            {
                var path = Path.Combine(dir, toolFileName);
                if (File.Exists(path))
                    return path;
            }
            
            return Option<string>.None;
        }
    }
}