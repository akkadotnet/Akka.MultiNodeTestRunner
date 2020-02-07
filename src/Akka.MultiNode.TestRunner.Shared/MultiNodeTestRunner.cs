//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
using Akka.Remote.TestKit;
using Akka.Util;
using Newtonsoft.Json;
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
    public class MultiNodeTestRunner
    {
        private static readonly HashSet<string> ValidNetCorePlatform = new HashSet<string>
        {
            "net",
            "netcore"
        };
        
        protected ActorSystem TestRunSystem;
        protected IActorRef SinkCoordinator;

        public Action<string> TestStarted;
        public Action<MultiNodeTestResult> TestFinished;

        /// <summary>
        /// Executes multi-node tests from given assembly
        /// </summary>
        public List<MultiNodeTestResult> Execute(string assemblyPath, MultiNodeTestRunnerOptions options)
        {
            ValidatePlatform(options);

            // Perform output cleanup before anything is logged
            if (options.ClearOutputDirectory && Directory.Exists(options.OutputDirectory))
                Directory.Delete(options.OutputDirectory, true);

            TestRunSystem = ActorSystem.Create("TestRunnerLogging");

            var suiteName = Path.GetFileNameWithoutExtension(Path.GetFullPath(assemblyPath));
            SinkCoordinator = CreateSinkCoordinator(options, suiteName);

            var tcpLogger = TestRunSystem.ActorOf(Props.Create(() => new TcpLoggingServer(SinkCoordinator)), "TcpLogger");
            var listenEndpoint = new IPEndPoint(IPAddress.Parse(options.ListenAddress), options.ListenPort);
            Console.WriteLine($"Binding MNTR to [{listenEndpoint}]");
            TestRunSystem.Tcp().Tell(new Tcp.Bind(tcpLogger, listenEndpoint), sender: tcpLogger);

            EnableAllSinks(assemblyPath, options);

            // Set MNTR environment for correct tests discovert
            MultiNodeEnvironment.Initialize();

            // In NetCore, if the assembly file hasn't been touched, 
            // XunitFrontController would fail loading external assemblies and its dependencies.
            PreLoadTestAssembly_WhenNetCore(assemblyPath);

            // Here is where main action goes
            var results = DiscoverAndRunSpecs(assemblyPath, options, tcpLogger);

            AbortTcpLoggingServer(tcpLogger);
            CloseAllSinks();

            // Block until all Sinks have been terminated.
            TestRunSystem.WhenTerminated.Wait(TimeSpan.FromMinutes(1));

            // Return the proper exit code
            return results;
        }

        /// <summary>
        /// Discovers all tests in given assembly
        /// </summary>
        public static (List<MultiNodeSpec> Specs, List<ErrorMessage> Errors) DiscoverSpecs(string assemblyPath)
        {
            MultiNodeEnvironment.Initialize();

            using (var controller = new XunitFrontController(AppDomainSupport.IfAvailable, assemblyPath))
            {
                using (var discovery = new Discovery())
                {
                    controller.Find(false, discovery, TestFrameworkOptions.ForDiscovery());
                    discovery.Finished.WaitOne();

                    if (!discovery.WasSuccessful)
                    {
                        return (Specs: new List<MultiNodeSpec>(), Errors: discovery.Errors);
                    }

                    var specs = discovery.Tests.Reverse().Select(pair => new MultiNodeSpec(pair.Key, pair.Value)).ToList();
                    return (specs, new List<ErrorMessage>());
                }
            }
        }

        private List<MultiNodeTestResult> DiscoverAndRunSpecs(string assemblyPath, MultiNodeTestRunnerOptions options, IActorRef tcpLogger)
        {
            var testResults = new List<MultiNodeTestResult>();
            PublishRunnerMessage($"Running MultiNodeTests for {assemblyPath}");

            var (discoveredSpecs, errors) = DiscoverSpecs(assemblyPath);
            if (errors.Any())
            {
                ReportDiscoveryErrors(errors);
                return testResults;
            }
            
            // If port was set random, request the actual port from TcpLoggingServer
            var listenPort = options.ListenPort > 0 
                ? options.ListenPort 
                : tcpLogger.Ask<int>(TcpLoggingServer.GetBoundPort.Instance).Result;
            
            foreach (var spec in discoveredSpecs)
            {
                var testName = spec.Tests.First().MethodName;
                TestStarted?.Invoke(spec.SpecName);
                    
                if (!string.IsNullOrEmpty(spec.FirstTest.SkipReason))
                {
                    PublishRunnerMessage($"Skipping test {testName}. Reason - {spec.FirstTest.SkipReason}");
                    var skippedResult = new MultiNodeTestResult(testName, MultiNodeTestResult.TestStatus.Skipped);
                    testResults.Add(skippedResult);
                    TestFinished?.Invoke(skippedResult);
                    continue;
                }

                if (options.SpecNames != null &&
                    !options.SpecNames.All(name => spec.FirstTest.TestName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) < 0))
                {
                    PublishRunnerMessage($"Skipping [{spec.FirstTest.MethodName}] (Filtering)");
                    var skippedResult = new MultiNodeTestResult(testName, MultiNodeTestResult.TestStatus.Skipped);
                    testResults.Add(skippedResult);
                    TestFinished?.Invoke(skippedResult);
                    continue;
                }

                // Run test on several nodes and report results
                var result = RunSpec(assemblyPath, options, spec, listenPort);
                TestFinished?.Invoke(result);
                testResults.Add(result);
            }

            return testResults;
        }

        private MultiNodeTestResult RunSpec(string assemblyPath, MultiNodeTestRunnerOptions options, MultiNodeSpec spec, int listenPort)
        {
            PublishRunnerMessage($"Starting test {spec.FirstTest.MethodName}");
            Console.Out.WriteLine($"Starting test {spec.FirstTest.MethodName}");

            StartNewSpec(spec.Tests);

            var timelineCollector = TestRunSystem.ActorOf(Props.Create(() => new TimelineLogCollectorActor()));
            string testOutputDir = null;
            string runningSpecName = null;

            var processes = new List<Process>();
            foreach (var nodeTest in spec.Tests)
            {
                //Loop through each test, work out number of nodes to run on and kick off process
                var sbArguments = new StringBuilder()
                    .Append($@"-Dmultinode.test-class=""{nodeTest.TypeName}"" ")
                    .Append($@"-Dmultinode.test-method=""{nodeTest.MethodName}"" ")
                    .Append($@"-Dmultinode.max-nodes={spec.Tests.Count} ")
                    .Append($@"-Dmultinode.server-host=""{"localhost"}"" ")
                    .Append($@"-Dmultinode.host=""{"localhost"}"" ")
                    .Append($@"-Dmultinode.index={nodeTest.Node - 1} ")
                    .Append($@"-Dmultinode.role=""{nodeTest.Role}"" ")
                    .Append($@"-Dmultinode.listen-address={options.ListenAddress} ")
                    .Append($@"-Dmultinode.listen-port={listenPort} ");

                // Configure process for node
                var process = BuildNodeProcess(assemblyPath, options, sbArguments);
                processes.Add(process);

                runningSpecName = nodeTest.TestName;

                //TODO: might need to do some validation here to avoid the 260 character max path error on Windows
                var folder = Directory.CreateDirectory(Path.Combine(options.OutputDirectory, nodeTest.TestName));
                testOutputDir = testOutputDir ?? folder.FullName;

                // Start process for node
                StartNodeProcess(process, nodeTest, options, folder, timelineCollector);
            }

            // Wait for all nodes to finish and collect results
            var specFailed = WaitForNodeExit(processes);

            PublishRunnerMessage("Waiting 3 seconds for all messages from all processes to be collected.");
            Thread.Sleep(TimeSpan.FromSeconds(3));

            // Save timelined logs to file system
            DumpAggregatedSpecLogs(options, testOutputDir, timelineCollector, specFailed, runningSpecName);

            FinishSpec(spec.Tests, timelineCollector);
            
            return new MultiNodeTestResult(runningSpecName, specFailed ? MultiNodeTestResult.TestStatus.Failed : MultiNodeTestResult.TestStatus.Passed);
        }

        private void DumpAggregatedSpecLogs(MultiNodeTestRunnerOptions options, string testOutputDir,
                                                   IActorRef timelineCollector, bool specFailed, string runningSpecName)
        {
            if (testOutputDir == null) return;
            var dumpTasks = new List<Task>()
            {
                // Dump aggregated timeline to file for this test
                timelineCollector.Ask<Done>(new TimelineLogCollectorActor.DumpToFile(Path.Combine(testOutputDir, "aggregated.txt"))),
                // Print aggregated timeline into the console
                timelineCollector.Ask<Done>(new TimelineLogCollectorActor.PrintToConsole())
            };

            if (specFailed)
            {
                var failedSpecPath = Path.Combine(Path.GetFullPath(options.OutputDirectory), options.FailedSpecsDirectory, $"{runningSpecName}.txt");
                var dumpFailureArtifactTask = timelineCollector.Ask<Done>(new TimelineLogCollectorActor.DumpToFile(failedSpecPath));
                dumpTasks.Add(dumpFailureArtifactTask);
            }

            Task.WaitAll(dumpTasks.ToArray());
        }

        private bool WaitForNodeExit(List<Process> processes)
        {
            var specFailed = false;
            foreach (var process in processes)
            {
                process.WaitForExit();
                specFailed = specFailed || process.ExitCode > 0;
                process.Dispose();
            }

            return specFailed;
        }

        private void StartNodeProcess(Process process, NodeTest nodeTest, MultiNodeTestRunnerOptions options, 
                                             DirectoryInfo specFolder, IActorRef timelineCollector)
        {
            var nodeIndex = nodeTest.Node;
            var nodeRole = nodeTest.Role;
            var logFilePath = Path.Combine(specFolder.FullName, $"node{nodeIndex}__{nodeRole}__{options.Platform}.txt");
            var nodeInfo = new TimelineLogCollectorActor.NodeInfo(nodeIndex, nodeRole, options.Platform, nodeTest.TestName);
            var fileActor = TestRunSystem.ActorOf(Props.Create(() => new FileSystemAppenderActor(logFilePath)));
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
            var closureTest = nodeTest;
            process.Exited += (sender, eventArgs) =>
            {
                if (process.ExitCode == 0)
                {
                    ReportSpecPassFromExitCode(nodeIndex, nodeRole, closureTest.TestName);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            PublishRunnerMessage($"Started node {nodeIndex} : {nodeRole} on pid {process.Id}");
        }

        private Process BuildNodeProcess(string assemblyPath, MultiNodeTestRunnerOptions options, StringBuilder sbArguments)
        {
#if CORECLR
            var ntrNetPath = Path.Combine(AppContext.BaseDirectory, "Akka.MultiNode.NodeRunner.exe");
            var ntrNetCorePath = Path.Combine(AppContext.BaseDirectory, "Akka.MultiNode.NodeRunner.dll");

            string fileName;
            switch (options.Platform)
            {
                case "net":
                    fileName = ntrNetPath;
                    sbArguments.Insert(0, $@" -Dmultinode.test-assembly=""{assemblyPath}"" ");
                    break;
                case "netcore":
                    fileName = "dotnet";
                    sbArguments.Insert(0, $@" -Dmultinode.test-assembly=""{assemblyPath}"" ");
                    sbArguments.Insert(0, ntrNetCorePath);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }

            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Arguments = sbArguments.ToString(),
                    WorkingDirectory = Path.GetDirectoryName(assemblyPath)
                }
            };
#else
            const string nodeRunnerFileName = "Akka.MultiNode.NodeRunner.exe";
            var searchPaths = new []
            {
                AppContext.BaseDirectory,
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            };
            
            // Under 'dotnet test' or as standalone util location of node runner is different.
            // The most robust way is to just scan possible locations and find it
            var ntrNetPath = FileToolInPaths(nodeRunnerFileName, dirPaths: searchPaths);
            if (!ntrNetPath.HasValue)
                throw new Exception($"Failed to find node runner '{nodeRunnerFileName}' at paths: {string.Join(", ", searchPaths)}");
            
            sbArguments.Insert(0, $@"-Dmultinode.test-assembly=""{assemblyPath}"" ");
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ntrNetPath.Value,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Arguments = sbArguments.ToString()
                }
            };
#endif
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
                        Console.Out.WriteLine($"Failed to load dll: {path}");
                    }
                }
            }
#endif
        }

        private void ValidatePlatform(MultiNodeTestRunnerOptions options)
        {
#if CORECLR
            if (!ValidNetCorePlatform.Contains(options.Platform))
            {
                throw new Exception($"Target platform not supported: {options.Platform}. Supported platforms are net and netcore");
            }
#else
            if (options.Platform != "net")
            {
                throw new Exception($"Target platform not supported: {options.Platform}. Supported platforms are net");
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
                        $"Given reporter name '{options.Reporter}' is not understood, valid reporters are: trx and teamcity");
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
                var fileName = FileNameGenerator.GenerateFileName(outputDirectory, assemblyName, options.Platform, ".json", now);
                var jsonStoreProps = Props.Create(() => new FileSystemMessageSinkActor(new JsonPersistentTestRunStore(), fileName, !options.TeamCityFormattingOn, true));
                return new FileSystemMessageSink(jsonStoreProps);
            }

            MessageSink CreateVisualizerFileSink()
            {
                var fileName = FileNameGenerator.GenerateFileName(outputDirectory, assemblyName, options.Platform, ".html", now);
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

        private void AbortTcpLoggingServer(IActorRef tcpLogger)
        {
            tcpLogger.Ask<TcpLoggingServer.ListenerStopped>(new TcpLoggingServer.StopListener(), TimeSpan.FromMinutes(1)).Wait();
        }

        private void CloseAllSinks()
        {
            SinkCoordinator.Tell(new SinkCoordinator.CloseAllSinks());
        }

        private void StartNewSpec(IList<NodeTest> tests)
        {
            SinkCoordinator.Tell(tests);
        }

        private void ReportSpecPassFromExitCode(int nodeIndex, string nodeRole, string testName)
        {
            SinkCoordinator.Tell(new NodeCompletedSpecWithSuccess(nodeIndex, nodeRole, testName + " passed."));
        }

        private void FinishSpec(IList<NodeTest> tests, IActorRef timelineCollector)
        {
            var spec = tests.First();
            var log = timelineCollector.Ask<SpecLog>(new TimelineLogCollectorActor.GetSpecLog(), TimeSpan.FromMinutes(1)).Result;
            SinkCoordinator.Tell(new EndSpec(spec.TestName, spec.MethodName, log));
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