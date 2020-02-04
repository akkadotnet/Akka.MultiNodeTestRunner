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
using Akka.Event;
using Akka.IO;
using Akka.MultiNode.Shared;
using Akka.MultiNode.Shared.Persistence;
using Akka.MultiNode.Shared.Sinks;
using Akka.MultiNode.Shared.TrxReporter;
using Akka.Remote.TestKit;
using Xunit;

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
        private static HashSet<string> _validNetCorePlatform = new HashSet<string>
        {
            "net",
            "netcore"
        };
        
        protected static ActorSystem TestRunSystem;
        protected static IActorRef SinkCoordinator;
        /// <summary>
        /// Subdirectory to store failed specs logs
        /// </summary>
        protected static string FailedSpecsDirectory;

        protected static bool MultiPlatform;

        /// <summary>
        /// MultiNodeTestRunner takes the following <see cref="args"/>:
        /// 
        /// C:\> Akka.MultiNode.TestRunner.exe [assembly name] [-Dmultinode.enable-filesink=on] [-Dmultinode.output-directory={dir path}] [-Dmultinode.spec={spec name}]
        /// 
        /// <list type="number">
        /// <listheader>
        ///     <term>Argument</term>
        ///     <description>The name and possible value of a given Akka.MultiNode.TestRunner.exe argument.</description>
        /// </listheader>
        /// <item>
        ///     <term>AssemblyName</term>
        ///     <description>
        ///         The full path or name of an assembly containing as least one MultiNodeSpec in the current working directory.
        /// 
        ///         i.e. "Akka.Cluster.Tests.MultiNode.dll"
        ///              "C:\akka.net\src\Akka.Cluster.Tests\bin\Debug\Akka.Cluster.Tests.MultiNode.dll"
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.enable-filesink</term>
        ///     <description>Having this flag set means that the contents of this test run will be saved in the
        ///                 current working directory as a .JSON file.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.multinode.output-directory</term>
        ///     <description>Setting this flag means that any persistent multi-node test runner output files
        ///                  will be written to this directory instead of the default, which is the same folder
        ///                  as the test binary.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.listen-address={ip}</term>
        ///     <description>
        ///             Determines the address that this multi-node test runner will use to listen for log messages from
        ///             individual NodeTestRunner.exe processes.
        /// 
        ///             Defaults to 127.0.0.1
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.listen-port={port}</term>
        ///     <description>
        ///             Determines the port number that this multi-node test runner will use to listen for log messages from
        ///             individual NodeTestRunner.exe processes.
        /// 
        ///             Defaults to 6577
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.spec={spec name}</term>
        ///     <description>
        ///             Setting this flag means that only tests which contains the spec name will be executed
        ///             otherwise all tests will be executed
        ///     </description>
        /// </item>
        /// </list>
        /// </summary>
        public int Execute(string assemblyPath, MultiNodeTestRunnerOptions options)
        {
            var suiteName = Path.GetFileNameWithoutExtension(Path.GetFullPath(assemblyPath));
            var listenEndpoint = new IPEndPoint(IPAddress.Parse(options.ListenAddress), options.ListenPort);
            
            if (options.ClearOutputDirectory && Directory.Exists(options.OutputDirectory))
                Directory.Delete(options.OutputDirectory, true);
            
            TestRunSystem = ActorSystem.Create("TestRunnerLogging");

            Props coordinatorProps;
            switch (options.Reporter.ToLowerInvariant())
            {
                case "trx":
                    coordinatorProps = Props.Create(() => new SinkCoordinator(new[] { new TrxMessageSink(suiteName) }));
                    break;

                case "teamcity":
                    coordinatorProps = Props.Create(() =>  new SinkCoordinator(new[] { new TeamCityMessageSink(Console.WriteLine, suiteName) }));
                    break;

                case "console":
                    coordinatorProps = Props.Create(() =>  new SinkCoordinator(new[] { new ConsoleMessageSink() }));
                    break;

                default:
                    throw new ArgumentException($"Given reporter name '{options.Reporter}' is not understood, valid reporters are: trx and teamcity");
            }

            SinkCoordinator = TestRunSystem.ActorOf(coordinatorProps, "sinkCoordinator");

#if CORECLR
            if (!_validNetCorePlatform.Contains(options.Platform))
            {
                throw new Exception($"Target platform not supported: {options.Platform}. Supported platforms are net and netcore");
            }
#else
            if (options.Platform != "net")
            {
                throw new Exception($"Target platform not supported: {options.Platform}. Supported platforms are net");
            }
#endif

            var tcpLogger = TestRunSystem.ActorOf(Props.Create(() => new TcpLoggingServer(SinkCoordinator)), "TcpLogger");
            TestRunSystem.Tcp().Tell(new Tcp.Bind(tcpLogger, listenEndpoint), sender: tcpLogger);

            EnableAllSinks(assemblyPath, options.Platform, options);
            PublishRunnerMessage($"Running MultiNodeTests for {assemblyPath}");
#if CORECLR
            // In NetCore, if the assembly file hasn't been touched, 
            // XunitFrontController would fail loading external assemblies and its dependencies.
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

            using (var controller = new XunitFrontController(AppDomainSupport.IfAvailable, assemblyPath))
            {
                using (var discovery = new Discovery())
                {
                    controller.Find(false, discovery, TestFrameworkOptions.ForDiscovery());
                    discovery.Finished.WaitOne();

                    if (discovery.WasSuccessful)
                    {
                        foreach (var test in discovery.Tests.Reverse())
                        {
                            if (!string.IsNullOrEmpty(test.Value.First().SkipReason))
                            {
                                PublishRunnerMessage($"Skipping test {test.Value.First().MethodName}. Reason - {test.Value.First().SkipReason}");
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(options.SpecName) &&
                                CultureInfo.InvariantCulture.CompareInfo.IndexOf(test.Value.First().TestName,
                                    options.SpecName,
                                    CompareOptions.IgnoreCase) < 0)
                            {
                                PublishRunnerMessage($"Skipping [{test.Value.First().MethodName}] (Filtering)");
                                continue;
                            }

                            var processes = new List<Process>();

                            PublishRunnerMessage($"Starting test {test.Value.First().MethodName}");
                            Console.Out.WriteLine($"Starting test {test.Value.First().MethodName}");

                            StartNewSpec(test.Value);
#if CORECLR
                            var ntrNetPath = Path.Combine(AppContext.BaseDirectory, "Akka.MultiNode.NodeRunner.exe");
                            var ntrNetCorePath = Path.Combine(AppContext.BaseDirectory, "Akka.MultiNode.NodeRunner.dll");
                            var alternateIndex = 0;
#endif
                            var timelineCollector = TestRunSystem.ActorOf(Props.Create(() => new TimelineLogCollectorActor()));
                            string testOutputDir = null;
                            string runningSpecName = null;
                            
                            foreach (var nodeTest in test.Value)
                            {
                                //Loop through each test, work out number of nodes to run on and kick off process
                                var sbArguments = new StringBuilder()
                                    //.Append($@"-Dmultinode.test-assembly=""{assemblyPath}"" ")
                                    .Append($@"-Dmultinode.test-class=""{nodeTest.TypeName}"" ")
                                    .Append($@"-Dmultinode.test-method=""{nodeTest.MethodName}"" ")
                                    .Append($@"-Dmultinode.max-nodes={test.Value.Count} ")
                                    .Append($@"-Dmultinode.server-host=""{"localhost"}"" ")
                                    .Append($@"-Dmultinode.host=""{"localhost"}"" ")
                                    .Append($@"-Dmultinode.index={nodeTest.Node - 1} ")
                                    .Append($@"-Dmultinode.role=""{nodeTest.Role}"" ")
                                    .Append($@"-Dmultinode.listen-address={options.ListenAddress} ")
                                    .Append($@"-Dmultinode.listen-port={options.ListenPort} ");

#if CORECLR
                                string fileName = null;
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
                                }
                                var process = new Process
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
                                sbArguments.Insert(0, $@"-Dmultinode.test-assembly=""{assemblyPath}"" ");
                                var process = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "Akka.MultiNode.NodeRunner.exe",
                                        UseShellExecute = false,
                                        RedirectStandardOutput = true,
                                        Arguments = sbArguments.ToString()
                                    }
                                };
#endif

                                processes.Add(process);
                                var nodeIndex = nodeTest.Node;
                                var nodeRole = nodeTest.Role;

#if CORECLR
                            if (options.Platform == "netcore")
                            {
                                process.StartInfo.FileName = "dotnet";
                                process.StartInfo.Arguments = ntrNetCorePath + " " + process.StartInfo.Arguments;
                                process.StartInfo.WorkingDirectory = Path.GetDirectoryName(assemblyPath);
                            }
#endif

                                //TODO: might need to do some validation here to avoid the 260 character max path error on Windows
                                var folder = Directory.CreateDirectory(Path.Combine(options.OutputDirectory, nodeTest.TestName));
                                testOutputDir = testOutputDir ?? folder.FullName;
                                var logFilePath = Path.Combine(folder.FullName, $"node{nodeIndex}__{nodeRole}__{options.Platform}.txt");
                                runningSpecName = nodeTest.TestName;
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

                            var specFailed = false;
                            foreach (var process in processes)
                            {
                                process.WaitForExit();
                                specFailed = specFailed || process.ExitCode > 0;
                                process.Dispose();
                            }

                            PublishRunnerMessage("Waiting 3 seconds for all messages from all processes to be collected.");
                            Thread.Sleep(TimeSpan.FromSeconds(3));
                            
                            if (testOutputDir != null)
                            {
                                var dumpTasks = new List<Task>()
                                {
                                    // Dump aggregated timeline to file for this test
                                    timelineCollector.Ask<Done>(new TimelineLogCollectorActor.DumpToFile(Path.Combine(testOutputDir, "aggregated.txt"))),
                                    // Print aggregated timeline into the console
                                    timelineCollector.Ask<Done>(new TimelineLogCollectorActor.PrintToConsole())
                                };

                                if (specFailed)
                                {
                                    var dumpFailureArtifactTask = timelineCollector.Ask<Done>(
                                        new TimelineLogCollectorActor.DumpToFile(Path.Combine(Path.GetFullPath(options.OutputDirectory), FailedSpecsDirectory, $"{runningSpecName}.txt")));
                                    dumpTasks.Add(dumpFailureArtifactTask);
                                }
                                Task.WaitAll(dumpTasks.ToArray());
                            }
                            
                            FinishSpec(test.Value, timelineCollector);
                        }
                        Console.WriteLine("Complete");
                        PublishRunnerMessage("Waiting 5 seconds for all messages from all processes to be collected.");
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("One or more exception was thrown while discovering test cases. Test Aborted.");
                        foreach (var err in discovery.Errors)
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
                }
            }
            
            AbortTcpLoggingServer(tcpLogger);
            CloseAllSinks();

            //Block until all Sinks have been terminated.
            TestRunSystem.WhenTerminated.Wait(TimeSpan.FromMinutes(1));

            if (Debugger.IsAttached)
                Console.ReadLine(); //block when debugging

            //Return the proper exit code
            return ExitCodeContainer.ExitCode;
        }

        static string ChangeDllPathPlatform(string path, string targetPlatform)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), "..", targetPlatform, Path.GetFileName(path)));
        }

        static void EnableAllSinks(string assemblyName, string platform, MultiNodeTestRunnerOptions options)
        {
            var now = DateTime.UtcNow;

            // if multinode.output-directory wasn't specified, the results files will be written
            // to the same directory as the test assembly.
            var outputDirectory = options.OutputDirectory;

            MessageSink CreateJsonFileSink()
            {
                var fileName = FileNameGenerator.GenerateFileName(outputDirectory, assemblyName, platform, ".json", now);
                var jsonStoreProps = Props.Create(() => new FileSystemMessageSinkActor(new JsonPersistentTestRunStore(), fileName, !options.TeamCityFormattingOn, true));
                return new FileSystemMessageSink(jsonStoreProps);
            }

            MessageSink CreateVisualizerFileSink()
            {
                var fileName = FileNameGenerator.GenerateFileName(outputDirectory, assemblyName, platform, ".html", now);
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

        private static void AbortTcpLoggingServer(IActorRef tcpLogger)
        {
            tcpLogger.Ask<TcpLoggingServer.ListenerStopped>(new TcpLoggingServer.StopListener(), TimeSpan.FromMinutes(1)).Wait();
        }

        private static void CloseAllSinks()
        {
            SinkCoordinator.Tell(new SinkCoordinator.CloseAllSinks());
        }

        private static void StartNewSpec(IList<NodeTest> tests)
        {
            SinkCoordinator.Tell(tests);
        }

        private static void ReportSpecPassFromExitCode(int nodeIndex, string nodeRole, string testName)
        {
            SinkCoordinator.Tell(new NodeCompletedSpecWithSuccess(nodeIndex, nodeRole, testName + " passed."));
        }

        private static void FinishSpec(IList<NodeTest> tests, IActorRef timelineCollector)
        {
            var spec = tests.First();
            var log = timelineCollector.Ask<SpecLog>(new TimelineLogCollectorActor.GetSpecLog(), TimeSpan.FromMinutes(1)).Result;
            SinkCoordinator.Tell(new EndSpec(spec.TestName, spec.MethodName, log));
        }

        private static void PublishRunnerMessage(string message)
        {
            SinkCoordinator.Tell(new SinkCoordinator.RunnerMessage(message));
        }

        private static void PublishToAllSinks(string message)
        {
            SinkCoordinator.Tell(message, ActorRefs.NoSender);
        }
    }

    internal class TcpLoggingServer : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private IActorRef _tcpManager = Nobody.Instance;
        private IActorRef _abortSender;

        public TcpLoggingServer(IActorRef sinkCoordinator)
        {
            Receive<Tcp.Bound>(_ => _tcpManager = Sender);
            Receive<Tcp.Connected>(connected =>
            {
                _log.Info($"Node connected on {Sender}");
                Sender.Tell(new Tcp.Register(Self));
            });

            Receive<Tcp.ConnectionClosed>(
                closed => _log.Info($"Node disconnected on {Sender}{Environment.NewLine}"));

            Receive<Tcp.Received>(received =>
            {
                var message = received.Data.ToString();
                sinkCoordinator.Tell(message);
            });

            Receive<StopListener>(_ =>
            {
                _abortSender = Sender;
                _tcpManager.Tell(Tcp.Unbind.Instance);
            });
            Receive<Tcp.Unbound>(_ => _abortSender.Tell(new ListenerStopped()));
        }
        
        public class StopListener { }
        public class ListenerStopped { }
    }
}