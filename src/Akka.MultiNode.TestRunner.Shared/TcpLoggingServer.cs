using System;
using Akka.Actor;
using Akka.Event;
using Akka.IO;

namespace Akka.MultiNode.TestRunner.Shared
{
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