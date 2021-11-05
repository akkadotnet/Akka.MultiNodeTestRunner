//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.IO;
using Akka.MultiNode.TestAdapter.Internal.Persistence;
using Akka.MultiNode.TestAdapter.Internal.Sinks;
using Akka.MultiNode.TestAdapter.Internal.TrxReporter;
using Akka.MultiNode.TestAdapter.Configuration;
using Xunit.Sdk;

namespace Akka.MultiNode.TestAdapter.Internal
{
    /// <summary>
    /// Entry point for the MultiNodeTestRunner
    /// </summary>
    internal class MultiNodeTestCaseRunner : TestCaseRunner<MultiNodeTestCase>
    {
        // Fixed TCP buffer size
        public const int TcpBufferSize = 10240;
        
        private ActorSystem TestRunSystem { get; set; }
        private IActorRef SinkCoordinator { get; set; }
        private int ListenPort { get; set; }
        private MultiNodeTestRunnerOptions Options { get; }
        
        /// <summary>
        /// Gets or sets the display name of the test case
        /// </summary>
        private string DisplayName { get; }

        /// <summary>
        /// Gets or sets the skip reason for the test, if set.
        /// </summary>
        private string SkipReason { get; }

        /// <summary>
        /// Gets or sets the runtime type for the test class that the test method belongs to.
        /// </summary>
        private Type TestClass { get; }

        /// <summary>
        /// Gets of sets the runtime method for the test method that the test case belongs to.
        /// </summary>
        private MethodInfo TestMethod { get; }

        public MultiNodeTestCaseRunner(
            MultiNodeTestCase testCase,
            string displayName,
            string skipReason,
            IMessageBus messageBus,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource) 
            : base(testCase, messageBus, aggregator, cancellationTokenSource)
        {
            DisplayName = displayName;
            SkipReason = skipReason;

            TestClass = TestCase.TestMethod.TestClass.Class.ToRuntimeType();
            TestMethod = TestCase.Method.ToRuntimeMethod();
            
            var assembly = TestClass.Assembly;
            var attr = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            var frameworkParts = attr.FrameworkName.Split(',');
            var versionParts = frameworkParts[1].Split('=');
            var platformName = (frameworkParts[0].Replace(".", "") + versionParts[1].Replace("v", "").Replace(".", "_")).ToLowerInvariant();
            Options = OptionsReader.Load(testCase.AssemblyPath);
            Options.Platform = platformName;
        }

        protected override async Task<RunSummary> RunTestAsync()
        {
            var config = ConfigurationFactory.ParseString($@"
akka.io.tcp {{
    buffer-pool = ""akka.io.tcp.disabled-buffer-pool""
    disabled-buffer-pool.buffer-size = {TcpBufferSize}
}}
");
            TestRunSystem = ActorSystem.Create("TestRunnerLogging", config);

            var suiteName = Path.GetFileNameWithoutExtension(Path.GetFullPath(TestCase.AssemblyPath));
            SinkCoordinator = CreateSinkCoordinator(Options, suiteName);

            var tcpLogger = TestRunSystem.ActorOf(Props.Create(() => new TcpLoggingServer(SinkCoordinator)), "TcpLogger");
            var listenEndpoint = new IPEndPoint(IPAddress.Parse(Options.ListenAddress), Options.ListenPort);
            TestRunSystem.Tcp().Tell(new Tcp.Bind(tcpLogger, listenEndpoint), sender: tcpLogger);

            // EnableAllSinks(TestCase.AssemblyPath, Options);
            
            // If port was set random, request the actual port from TcpLoggingServer
            ListenPort = Options.ListenPort > 0 
                ? Options.ListenPort 
                : tcpLogger.Ask<int>(TcpLoggingServer.GetBoundPort.Instance).Result;

            PublishRunnerMessage($"Starting test {TestCase.MethodName}");
            StartNewSpec();

            var timelineCollector = TestRunSystem.ActorOf(Props.Create(() => new TimelineLogCollectorActor()));
            //TODO: might need to do some validation here to avoid the 260 character max path error on Windows
            var folder = Directory.CreateDirectory(Path.Combine(Options.OutputDirectory, TestCase.MethodName));
            var testOutputDir = folder.FullName;

            var tasks = new List<Task<RunSummary>>();
            foreach (var nodeTest in TestCase.Nodes)
            {
                //Loop through each test, work out number of nodes to run on and kick off process
                var args = new []
                    {
                        $@"-Dmultinode.test-class=""{nodeTest.TestCase.TypeName}""",
                        $@"-Dmultinode.test-method=""{nodeTest.TestCase.MethodName}""",
                        $@"-Dmultinode.max-nodes={TestCase.Nodes.Count}",
                        $@"-Dmultinode.server-host=""{"localhost"}""",
                        $@"-Dmultinode.host=""{"localhost"}""",
                        $@"-Dmultinode.index={nodeTest.Node - 1}",
                        $@"-Dmultinode.role=""{nodeTest.Role}""",
                        $@"-Dmultinode.listen-address={Options.ListenAddress}",
                        $@"-Dmultinode.listen-port={ListenPort}",
                        $@"-Dmultinode.test-assembly=""{TestCase.AssemblyPath}"""
                    };

                // Start process for node
                var runner = new MultiNodeTestRunner(
                    nodeTest, MessageBus, args, SkipReason, Aggregator, SinkCoordinator,
                    timelineCollector, Options, CancellationTokenSource);
                
                tasks.Add(runner.RunAsync());
            }

            var summary = new RunSummary();
            // Wait for all nodes to finish and collect results
            while (tasks.Count > 0)
            {
                var finished = await Task.WhenAny(tasks);
                tasks.Remove(finished);
                summary.Aggregate(finished.Result);
            }

            // Save timelined logs to file system
            await DumpAggregatedSpecLogs(summary, Options, testOutputDir, timelineCollector);
            
            await FinishSpec(timelineCollector);

            if(tcpLogger != null)
                await tcpLogger.Ask<TcpLoggingServer.ListenerStopped>(new TcpLoggingServer.StopListener(), TimeSpan.FromMinutes(1));
            
            SinkCoordinator.Tell(new SinkCoordinator.CloseAllSinks());

            // Block until all Sinks have been terminated.
            var shutdown = CoordinatedShutdown.Get(TestRunSystem);
            await shutdown.Run(CoordinatedShutdown.ActorSystemTerminateReason.Instance);

            return summary;
        }

        private async Task DumpAggregatedSpecLogs(
            RunSummary summary,
            MultiNodeTestRunnerOptions options, 
            string testOutputDir,
            IActorRef timelineCollector)
        {
            if (testOutputDir == null) return;

            var dumpPath = Path.GetFullPath(Path.Combine(testOutputDir, "aggregated.txt"));
            var dumpTasks = new List<Task>()
            {
                // Dump aggregated timeline to file for this test
                timelineCollector.Ask<Done>(new TimelineLogCollectorActor.DumpToFile(dumpPath)),
            };

            if (summary.Failed > 0)
            {
                var failedSpecPath = Path.GetFullPath(Path.Combine(options.OutputDirectory, options.FailedSpecsDirectory, $"{TestCase.MethodName}.txt"));
                var dumpFailureArtifactTask = timelineCollector.Ask<Done>(new TimelineLogCollectorActor.DumpToFile(failedSpecPath));
                dumpTasks.Add(dumpFailureArtifactTask);
            }

            await Task.WhenAll(dumpTasks);
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
                var fileName = FileNameGenerator.GenerateFileName(outputDirectory, assemblyName, Options.Platform, ".json", now);
                var jsonStoreProps = Props.Create(() => new FileSystemMessageSinkActor(new JsonPersistentTestRunStore(), fileName, !options.TeamCityFormattingOn, true));
                return new FileSystemMessageSink(jsonStoreProps);
            }

            MessageSink CreateVisualizerFileSink()
            {
                var fileName = FileNameGenerator.GenerateFileName(outputDirectory, assemblyName, Options.Platform, ".html", now);
                var visualizerProps = Props.Create(() => new FileSystemMessageSinkActor(new VisualizerPersistentTestRunStore(), fileName, !options.TeamCityFormattingOn, true));
                return new FileSystemMessageSink(visualizerProps);
            }

            SinkCoordinator.Tell(new SinkCoordinator.EnableSink(CreateJsonFileSink()));
            SinkCoordinator.Tell(new SinkCoordinator.EnableSink(CreateVisualizerFileSink()));
        }

        private void StartNewSpec()
        {
            SinkCoordinator.Tell(TestCase);
        }

        private async Task FinishSpec(IActorRef timelineCollector)
        {
            var log = await timelineCollector.Ask<SpecLog>(new TimelineLogCollectorActor.GetSpecLog(), TimeSpan.FromMinutes(1));
            SinkCoordinator.Tell(new EndSpec(TestCase, log));
        }

        private void PublishRunnerMessage(string message)
        {
            SinkCoordinator.Tell(new SinkCoordinator.RunnerMessage(message));
        }
    }
}