using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EventStore.Core.Tests.Helper
{
    public static class TcpPortsHelper
    {
        private static readonly EventStore.Common.Concurrent.ConcurrentQueue<int> AvailablePorts = 
            new EventStore.Common.Concurrent.ConcurrentQueue<int>(GetRandomPorts(45000, 10000));

        public static int GetAvailablePort(IPAddress ip)
        {
            for (int i = 0; i < 50; ++i)
            {
                int port;
                if (!AvailablePorts.TryDequeue(out port))
                    throw new Exception("Couldn't get free TCP port for MiniNode.");
                try
                {
                    var listener = new TcpListener(ip, port);
                    listener.Start();

                    listener.Stop();
                    return port;
                }
                catch (Exception)
                {
                    AvailablePorts.Enqueue(port);
                }
            }
            throw new Exception("Reached trials limit while trying to get free port for MiniNode");
        }

        public static void ReturnPort(int port)
        {
            AvailablePorts.Enqueue(port);
        }

        private static int[] GetRandomPorts(int from, int portCount)
        {
            var res = new int[portCount];
            var rnd = new Random(Math.Abs(Guid.NewGuid().GetHashCode()));
            for (int i = 0; i < portCount; ++i)
            {
                res[i] = from + i;
            }
            for (int i = 0; i < portCount; ++i)
            {
                int index = rnd.Next(portCount - i);
                int tmp = res[i];
                res[i] = res[i + index];
                res[i + index] = tmp;
            }
            return res;
        }
    }
}
