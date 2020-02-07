using System.Net;

namespace Akka.MultiNode.TestRunner.Shared.Helpers
{
    /// <summary>
    /// TcpHelper
    /// </summary>
    public class TcpHelper
    {
        /// <summary>
        /// Gets free TCP port on current local machine
        /// </summary>
        /// <returns></returns>
        public static int GetFreeTcpPort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}