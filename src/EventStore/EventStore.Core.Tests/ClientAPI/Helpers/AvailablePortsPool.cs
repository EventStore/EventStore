using System;
using System.Net;
using System.Net.Sockets;

namespace EventStore.Core.Tests.ClientAPI.Helpers
{
    public class AvailablePortsPool
    {
        private readonly EventStore.Common.Concurrent.ConcurrentQueue<int> _availablePorts;

        public AvailablePortsPool(int from, int portCount)
        {
            _availablePorts = new EventStore.Common.Concurrent.ConcurrentQueue<int>(GetRandomPorts(from, portCount));
        }

        public int GetAvailablePort(IPAddress ip)
        {
            for (int i = 0; i < 50; ++i)
            {
                int port;
                if (!_availablePorts.TryDequeue(out port))
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
                    _availablePorts.Enqueue(port);
                }
            }
            throw new Exception("Reached trials limit while trying to get free port for MiniNode");
        }

        public void ReleasePort(int port)
        {
            _availablePorts.Enqueue(port);
        }

        private int[] GetRandomPorts(int from, int portCount)
        {
            var res = new int[portCount];
            var rnd = new Random(Guid.NewGuid().GetHashCode());
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