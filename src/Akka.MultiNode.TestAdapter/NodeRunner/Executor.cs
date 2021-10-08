//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using Akka.Actor;
using Akka.IO;
using Akka.MultiNode.TestAdapter.Internal.Sinks;
using Akka.Remote.TestKit;
using Xunit;

namespace Akka.MultiNode.TestAdapter.NodeRunner
{
    public class Executor
    {
        /// <summary>
        /// If it takes longer than this value for the <see cref="ExecutorSink"/> to get back to us
        /// about a particular test passing or failing, throw loudly.
        /// </summary>
        public int Execute(string[] args)
        {
            var maxProcessWaitTimeout = TimeSpan.FromMinutes(5);
            IActorRef logger = null;

            try
            {
                CommandLine.Initialize(args);
                
                var nodeIndex = CommandLine.GetInt32("multinode.index");
                var nodeRole = CommandLine.GetProperty("multinode.role");
                var assemblyFileName = CommandLine.GetProperty("multinode.test-assembly");
                var typeName = CommandLine.GetProperty("multinode.test-class");
                var testName = CommandLine.GetProperty("multinode.test-method");
                var displayName = testName;

                var listenAddress = IPAddress.Parse(CommandLine.GetProperty("multinode.listen-address"));
                var listenPort = CommandLine.GetInt32("multinode.listen-port");
                var listenEndpoint = new IPEndPoint(listenAddress, listenPort);

                try
                {
                    var system = ActorSystem.Create("NoteTestRunner-" + nodeIndex);
                    var tcpClient = logger = system.ActorOf<RunnerTcpClient>();
                    system.Tcp().Tell(new Tcp.Connect(listenEndpoint), tcpClient);

                    // In NetCore, if the assembly file hasn't been touched, 
                    // XunitFrontController would fail loading external assemblies and its dependencies.
                    
                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyFileName);
                    var asms = assembly.GetReferencedAssemblies();
                    var basePath = Path.GetDirectoryName(assemblyFileName) ?? "";
                    var loadContext = AssemblyLoadContext.GetLoadContext(assembly);
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
                                loadContext.LoadFromAssemblyPath(path);
                            }
                            catch 
                            {
                                try
                                {
                                    var name = AssemblyLoadContext.GetAssemblyName(path);
                                    loadContext.LoadFromAssemblyName(name);
                                }
                                catch (Exception e)
                                {
                                    Console.Out.WriteLine($"Failed to load dll [{path}]: {e}");
                                }
                            }
                        }
                    }
                    
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                    
                    using (var controller = new XunitFrontController(AppDomainSupport.IfAvailable, assemblyFileName))
                    {
                        /* need to pass in just the assembly name to Discovery, not the full path
                         * i.e. "Akka.Cluster.Tests.MultiNode.dll"
                         * not "bin/Release/Akka.Cluster.Tests.MultiNode.dll" - this will cause
                         * the Discovery class to actually not find any individual specs to run
                         */
                        var assemblyName = Path.GetFileName(assemblyFileName);
                        Console.WriteLine("Running specs for {0} [{1}] ", assemblyName, assemblyFileName);
                        
                        using (var discovery = new Discovery(assemblyName))
                        {
                            using (var sink = new ExecutorSink(nodeIndex, nodeRole, tcpClient))
                            {
                                controller.Find(true, discovery, TestFrameworkOptions.ForDiscovery());
                                discovery.Finished.WaitOne();
                                var tests = discovery.TestCases
                                    .Where(t => t.TestMethod.Method.Name == testName && t.TestMethod.TestClass.Class.Name == typeName).ToList();
                                controller.RunTests(tests, sink, TestFrameworkOptions.ForExecution());

                                var timedOut = false;
                                if (!sink.Finished.WaitOne(maxProcessWaitTimeout)) //timed out
                                {
                                    var line =
                                        $"Timed out while waiting for test to complete after {maxProcessWaitTimeout} ms";
                                    logger.Tell(line);
                                    Console.WriteLine(line);
                                    timedOut = true;
                                }

                                FlushLogMessages();
                                system.Terminate().Wait();

                                var retCode = sink.Passed && !timedOut ? 0 : 1;
                                Environment.Exit(retCode);
                                return retCode;
                            }
                        }
                    }
                }
                catch (AggregateException ex)
                {
                    var specFail = new SpecFail(nodeIndex, nodeRole, displayName);
                    specFail.FailureExceptionTypes.Add(ex.GetType().ToString());
                    specFail.FailureMessages.Add(ex.Message);
                    specFail.FailureStackTraces.Add(ex.StackTrace);
                    foreach (var innerEx in ex.Flatten().InnerExceptions)
                    {
                        specFail.FailureExceptionTypes.Add(innerEx.GetType().ToString());
                        specFail.FailureMessages.Add(innerEx.Message);
                        specFail.FailureStackTraces.Add(innerEx.StackTrace);
                    }

                    logger?.Tell(specFail.ToString());
                    Console.WriteLine(specFail);

                    //make sure message is send over the wire
                    FlushLogMessages();
                    Environment.Exit(1); //signal failure
                    return 1;
                }
                catch (Exception ex)
                {
                    var specFail = new SpecFail(nodeIndex, nodeRole, displayName);
                    specFail.FailureExceptionTypes.Add(ex.GetType().ToString());
                    specFail.FailureMessages.Add(ex.Message);
                    specFail.FailureStackTraces.Add(ex.StackTrace);
                    var innerEx = ex.InnerException;
                    while (innerEx != null)
                    {
                        specFail.FailureExceptionTypes.Add(innerEx.GetType().ToString());
                        specFail.FailureMessages.Add(innerEx.Message);
                        specFail.FailureStackTraces.Add(innerEx.StackTrace);
                        innerEx = innerEx.InnerException;
                    }

                    logger?.Tell(specFail.ToString());
                    Console.WriteLine(specFail);

                    //make sure message is send over the wire
                    FlushLogMessages();
                    Environment.Exit(1); //signal failure
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected FATAL exception: {ex}");
                Environment.Exit(1); //signal failure
                return 1;
            }
            
            void FlushLogMessages()
            {
                try
                {
                    logger?.GracefulStop(TimeSpan.FromSeconds(2)).Wait();
                }
                catch
                {
                    Console.WriteLine("Exception thrown while waiting for TCP transport to flush - not all messages may have been logged.");
                }
            }
        }
    }

    class RunnerTcpClient : ReceiveActor, IWithUnboundedStash
    {
        private IActorRef _connection;
        
        public RunnerTcpClient()
        {
            Become(WaitingForConnection);
        }

        /// <inheritdoc />
        protected override void PostStop()
        {
            // Close connection property to avoid exception logged at TcpConnection actor once this actor is terminated
            try
            {
                _connection.Ask<Tcp.Closed>(Tcp.Close.Instance, TimeSpan.FromSeconds(1)).Wait();
            }
            catch { /* well... at least we have tried */ }
            
            base.PostStop();
        }

        private void WaitingForConnection()
        {
            Receive<Tcp.Connected>(connected =>
            {
                Sender.Tell(new Tcp.Register(Self));
                _connection = Sender;
                Become(Connected(Sender));
            });
            Receive<string>(_ => Stash.Stash());
        }

        private Receive Connected(IActorRef connection)
        {
            Stash.UnstashAll();

            return message =>
            {
                var bytes = ByteString.FromString(message.ToString());
                connection.Tell(Tcp.Write.Create(bytes));

                return true;
            };
        }

        public IStash Stash { get; set; }
    }
}

