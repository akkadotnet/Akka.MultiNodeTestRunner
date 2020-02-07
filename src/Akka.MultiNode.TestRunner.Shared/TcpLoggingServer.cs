using System;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using Akka.Actor;
using Akka.Event;
using Akka.IO;
using Akka.Util;
using Akka.Util.Internal;

namespace Akka.MultiNode.TestRunner.Shared
{
    internal class TcpLoggingServer : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private IActorRef _tcpManager = Nobody.Instance;
        private IActorRef _abortSender;

        private Option<int> _boundPort;
        private IImmutableSet<IActorRef> _boundPortSubscribers = ImmutableHashSet<IActorRef>.Empty;

        public TcpLoggingServer(IActorRef sinkCoordinator)
        {
            Receive<Tcp.Bound>(bound =>
            {
                // When bound, save port and notify requestors if any
                if (!(bound.LocalAddress is IPEndPoint ipEndPoint))
                {
                    _log.Error($"Expected local address to be IPEndpoint, but got type {bound.LocalAddress.GetType().FullName}");
                    throw new Exception($"Expected local address to be IPEndpoint, but got type {bound.LocalAddress.GetType().FullName}");
                }
                else
                {
                    _boundPort = ipEndPoint.Port;
                    _boundPortSubscribers.ForEach(s => s.Tell(_boundPort.Value));
                }
                
                _tcpManager = Sender;
            });
            
            Receive<GetBoundPort>(_ =>
            {
                // If bound port is not received yet, just save subscriber and send respose later
                if (_boundPort.HasValue)
                    Sender.Tell(_boundPort.Value);
                else
                    _boundPortSubscribers = _boundPortSubscribers.Add(Sender);
            });
            
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

        public class GetBoundPort
        {
            private GetBoundPort() { }
            public static readonly GetBoundPort Instance = new GetBoundPort();
        }
    }
}