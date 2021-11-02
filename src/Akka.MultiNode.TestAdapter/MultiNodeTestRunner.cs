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
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.IO;
using Akka.MultiNode.TestAdapter.Internal;
using Akka.MultiNode.TestAdapter.Internal.Persistence;
using Akka.MultiNode.TestAdapter.Internal.Sinks;
using Akka.MultiNode.TestAdapter.Internal.TrxReporter;
using Akka.MultiNode.TestAdapter.NodeRunner;
using Akka.Remote.TestKit;
using Xunit;
using ErrorMessage = Xunit.Sdk.ErrorMessage;

namespace Akka.MultiNode.TestAdapter
{
    /// <summary>
    /// Entry point for the MultiNodeTestRunner
    /// </summary>
    public class MultiNodeTestRunner : IDisposable
    {
        // Fixed TCP buffer size
        public const int TcpBufferSize = 10240;
        
        private string _platformName;

        private string _currentAssembly;
        private IActorRef _tcpLogger;
        
        protected ActorSystem TestRunSystem;
        protected IActorRef SinkCoordinator;

        public event Action<MultiNodeTestCase> TestStarted;
        public event Action<MultiNodeTestResult> TestPassed;
        public event Action<MultiNodeTestResult> TestFailed;
        public event Action<MultiNodeTestCase, string> TestSkipped;
        public event Action<MultiNodeTestCase, Exception> Exception;

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
            
            // In NetCore, if the assembly file hasn't been touched, 
            // XunitFrontController would fail loading external assemblies and its dependencies.
            var assembly = PreLoadTestAssembly(assemblyPath);
            var attr = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            var frameworkParts = attr.FrameworkName.Split(',');
            var versionParts = frameworkParts[1].Split('=');
            _platformName = (frameworkParts[0].Replace(".", "") + versionParts[1].Replace("v", "").Replace(".", "_")).ToLowerInvariant();
            Console.WriteLine($"Platform name: {_platformName}");
            
            _currentAssembly = fileName;

            var config = ConfigurationFactory.ParseString($@"
akka.io.tcp {{
    buffer-pool = ""akka.io.tcp.disabled-buffer-pool""
    disabled-buffer-pool.buffer-size = {TcpBufferSize}
}}
");
            TestRunSystem = ActorSystem.Create("TestRunnerLogging", config);

            var suiteName = Path.GetFileNameWithoutExtension(Path.GetFullPath(assemblyPath));
            SinkCoordinator = CreateSinkCoordinator(options, suiteName);

            _tcpLogger = TestRunSystem.ActorOf(Props.Create(() => new TcpLoggingServer(SinkCoordinator)), "TcpLogger");
            var listenEndpoint = new IPEndPoint(IPAddress.Parse(options.ListenAddress), options.ListenPort);
            TestRunSystem.Tcp().Tell(new Tcp.Bind(_tcpLogger, listenEndpoint), sender: _tcpLogger);

            EnableAllSinks(assemblyPath, options);
        }
        
        public MultiNodeTestResult ExecuteSpec(MultiNodeTestCase testCase, MultiNodeTestRunnerOptions options)
        {
            Initialize(testCase.AssemblyPath, options);
            
            // If port was set random, request the actual port from TcpLoggingServer
            var listenPort = options.ListenPort > 0 
                ? options.ListenPort 
                : _tcpLogger.Ask<int>(TcpLoggingServer.GetBoundPort.Instance).Result;

            TestStarted?.Invoke(testCase);
            Environment.SetEnvironmentVariable(MultiNodeFactAttribute.MultiNodeTestEnvironmentName, "1");
            try
            {
                if (options.SpecNames != null &&
                    options.SpecNames.All(name => testCase.DisplayName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) < 0))
                {
                    testCase.SkipReason = "Excluded by filtering";
                }
                
                if (!string.IsNullOrEmpty(testCase.SkipReason))
                {
                    PublishRunnerMessage($"Skipping [{testCase.MethodName}]. Reason: [{testCase.SkipReason}]");
                    TestSkipped?.Invoke(testCase, testCase.SkipReason);
                    return null;
                }
                
                // touch test.Nodes to load details
                var nodes = testCase.Nodes;

                var result = RunSpec(options, testCase, listenPort);
                switch (result.Status)
                {
                    case MultiNodeTestResult.TestStatus.Passed:
                        TestPassed?.Invoke(result);
                        return result;
                    case MultiNodeTestResult.TestStatus.Failed:
                        TestFailed?.Invoke(result);
                        return result;
                    case MultiNodeTestResult.TestStatus.Skipped:
                        TestSkipped?.Invoke(testCase, "Must be run using Akka.MultiNode.TestAdapter");
                        return null;
                }
                return result;
            }
            catch (Exception e)
            {
                Exception?.Invoke(testCase, e);
                PublishRunnerMessage(e.Message);
                return null;
            }
            finally
            {
                Environment.SetEnvironmentVariable(MultiNodeFactAttribute.MultiNodeTestEnvironmentName, null);
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
        public static (List<MultiNodeTestCase> Tests, List<ErrorMessage> Errors) DiscoverSpecs(string assemblyPath)
        {
            Environment.SetEnvironmentVariable(MultiNodeFactAttribute.MultiNodeTestEnvironmentName, "1");
            try
            {
                using (var controller = new XunitFrontController(AppDomainSupport.IfAvailable, assemblyPath))
                {
                    using (var discovery = new Discovery(assemblyPath))
                    {
                        controller.Find(false, discovery, TestFrameworkOptions.ForDiscovery());
                        discovery.Finished.WaitOne();
                        return (discovery.TestCases, discovery.Errors);
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(MultiNodeFactAttribute.MultiNodeTestEnvironmentName, null);
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
                        options.SpecNames.All(name => test.DisplayName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) < 0))
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
                    switch (result.Status)
                    {
                        case MultiNodeTestResult.TestStatus.Failed:
                            TestFailed?.Invoke(result);
                            testResults.Add(result);
                            break;
                        case MultiNodeTestResult.TestStatus.Passed:
                            TestPassed?.Invoke(result);
                            testResults.Add(result);
                            break;
                        case MultiNodeTestResult.TestStatus.Skipped:
                            TestSkipped?.Invoke(test, test.SkipReason);
                            continue;
                    }
                }
                catch (Exception e)
                {
                    Exception?.Invoke(test, e);
                    PublishRunnerMessage(e.Message);
                }
            }

            return testResults;
        }

        private MultiNodeTestResult RunSpec(MultiNodeTestRunnerOptions options, MultiNodeTestCase testCase, int listenPort)
        {
            PublishRunnerMessage($"Starting test {testCase.MethodName}");
            StartNewSpec(testCase);

            var timelineCollector = TestRunSystem.ActorOf(Props.Create(() => new TimelineLogCollectorActor()));
            //TODO: might need to do some validation here to avoid the 260 character max path error on Windows
            var folder = Directory.CreateDirectory(Path.Combine(options.OutputDirectory, testCase.DisplayName));
            var testOutputDir = folder.FullName;

            var testResult = new MultiNodeTestResult(testCase);
            
            var nodeProcesses = new List<(NodeTest, Process)>();
            foreach (var nodeTest in testCase.Nodes)
            {
                //Loop through each test, work out number of nodes to run on and kick off process
                var args = new []
                    {
                        $@"-Dmultinode.test-class=""{nodeTest.TestCase.TypeName}""",
                        $@"-Dmultinode.test-method=""{nodeTest.TestCase.MethodName}""",
                        $@"-Dmultinode.max-nodes={testCase.Nodes.Count}",
                        $@"-Dmultinode.server-host=""{"localhost"}""",
                        $@"-Dmultinode.host=""{"localhost"}""",
                        $@"-Dmultinode.index={nodeTest.Node - 1}",
                        $@"-Dmultinode.role=""{nodeTest.Role}""",
                        $@"-Dmultinode.listen-address={options.ListenAddress}",
                        $@"-Dmultinode.listen-port={listenPort}",
                        $@"-Dmultinode.test-assembly=""{testCase.AssemblyPath}""",
                        "-Dmultinode.test-runner=\"multinode\""
                    };

                // Configure process for node
                // var process = BuildNodeProcess(test.AssemblyPath, sbArguments);

                // Start process for node
                var process = StartNodeProcess(args, nodeTest, options, folder, timelineCollector, testResult);
                nodeProcesses.Add((nodeTest, process));
            }

            // Wait for all nodes to finish and collect results
            WaitForNodeExit(testResult, nodeProcesses);

            PublishRunnerMessage("Waiting 3 seconds for all messages from all processes to be collected.");
            Thread.Sleep(TimeSpan.FromSeconds(3));

            // Save timelined logs to file system
            DumpAggregatedSpecLogs(testResult, options, testOutputDir, timelineCollector);
            
            FinishSpec(testCase, timelineCollector);
            
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
                var failedSpecPath = Path.GetFullPath(Path.Combine(options.OutputDirectory, options.FailedSpecsDirectory, $"{result.TestCase.DisplayName}.txt"));
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
                    nodeResult.Result = process.ExitCode switch
                    {
                        0 => MultiNodeTestResult.TestStatus.Passed,
                        2 => MultiNodeTestResult.TestStatus.Skipped,
                        _ => MultiNodeTestResult.TestStatus.Failed
                    };
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
        
        private Process StartNodeProcess(
            //Process process, 
            string[] args,
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
            var nodeInfo = new TimelineLogCollectorActor.NodeInfo(nodeIndex, nodeRole, _platformName, nodeTest.TestCase.DisplayName);
            var fileActor = TestRunSystem.ActorOf(Props.Create(() => new FileSystemAppenderActor(logFilePath)));
            result.Attachments.Add(new MultiNodeTestResult.Attachment{Title = $"Node {nodeIndex} [{nodeRole}]", Path = logFilePath});

            var runner = new Executor();

            void OutputHandler(object sender, DataReceivedEventArgs eventArgs)
            {
                if (eventArgs?.Data != null)
                {
                    fileActor.Tell(eventArgs.Data);
                    timelineCollector.Tell(new TimelineLogCollectorActor.LogMessage(nodeInfo, eventArgs.Data));
                    Console.WriteLine(eventArgs.Data);
                    if (options.TeamCityFormattingOn)
                    {
                        // teamCityTest.WriteStdOutput(eventArgs.Data); TODO: open flood gates
                    }
                }
            }
            
            var process = RemoteHost.RemoteHost.Start(runner.Execute, args, opt =>
            {
                opt.OnExit = p =>
                {
                    if (p.ExitCode == 0)
                    {
                        ReportSpecPassFromExitCode(nodeIndex, nodeRole, closureTest.TestCase.MethodName);
                    }
                };
                opt.OutputDataReceived = OutputHandler;
                opt.ErrorDataReceived = OutputHandler;
            });
            
            PublishRunnerMessage($"Started node {nodeIndex} : {nodeRole} on pid {process.Id}");
            return process;
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

        private Assembly PreLoadTestAssembly(string assemblyPath)
        {
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

            return assembly;
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

            SinkCoordinator.Tell(new SinkCoordinator.EnableSink(CreateJsonFileSink()));
            SinkCoordinator.Tell(new SinkCoordinator.EnableSink(CreateVisualizerFileSink()));
        }

        private void AbortTcpLoggingServer()
        {
            _tcpLogger?.Ask<TcpLoggingServer.ListenerStopped>(new TcpLoggingServer.StopListener(), TimeSpan.FromMinutes(1)).Wait();
        }

        private void CloseAllSinks()
        {
            SinkCoordinator?.Tell(new SinkCoordinator.CloseAllSinks());
        }

        private void StartNewSpec(MultiNodeTestCase testCase)
        {
            SinkCoordinator.Tell(testCase);
        }

        private void ReportSpecPassFromExitCode(int nodeIndex, string nodeRole, string testName)
        {
            SinkCoordinator.Tell(new NodeCompletedSpecWithSuccess(nodeIndex, nodeRole, testName + " passed."));
        }

        private void FinishSpec(MultiNodeTestCase testCase, IActorRef timelineCollector)
        {
            var log = timelineCollector.Ask<SpecLog>(new TimelineLogCollectorActor.GetSpecLog(), TimeSpan.FromMinutes(1)).Result;
            SinkCoordinator.Tell(new EndSpec(testCase, log));
        }

        private void PublishRunnerMessage(string message)
        {
            SinkCoordinator.Tell(new SinkCoordinator.RunnerMessage(message));
        }

    }
}