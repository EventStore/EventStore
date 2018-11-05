﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using EventStore.Common.Log;
using System.Collections.Concurrent;

namespace EventStore.Core.Tests.Helpers
{
    public static class PortsHelper
    {
        private static readonly ILogger Log = LogManager.GetLogger("PortsHelper");

        public const int PortStart = 45000;
        public const int PortCount = 200;

        private static readonly ConcurrentQueue<int> AvailablePorts = 
            new ConcurrentQueue<int>(Enumerable.Range(PortStart, PortCount));

        public static void InitPorts(IPAddress ip)
        {
            var sw = Stopwatch.StartNew();

            int p;
            while (AvailablePorts.TryDequeue(out p))
            {
            }

            Log.Trace("PortsHelper: starting to examine ports at [{0}].", ip);

            int succ = 0;
            for (int port = PortStart; port < PortStart + PortCount; ++port)
            {
                try
                {
                    var listener = new TcpListener(ip, port);
                    listener.Start();
                    listener.Stop();
                }
                catch (Exception exc)
                {
                    Log.TraceException(exc, "PortsHelper: port {0} unavailable for TcpListener. Error: {1}.", port, exc.Message);
                    continue;
                }

                try
                {
                    var httpListener = new HttpListener();
                    httpListener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", port));
                    httpListener.Start();

                    Exception httpListenerError = null;
                    var listenTask = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var context = httpListener.GetContext();
                            context.Response.Close(new byte[] {1, 2, 3}, true);
                        }
                        catch (Exception exc)
                        {
                            httpListenerError = exc;
                        }
                    });

                    var request = (HttpWebRequest)WebRequest.Create(string.Format("http://{0}:{1}/", ip, port));
                    var buffer = new byte[256];
                    var read = request.GetResponse().GetResponseStream().Read(buffer, 0, buffer.Length);
                    if (read != 3 || buffer[0] != 1 || buffer[1] != 2 || buffer[2] != 3)
                        throw new Exception(string.Format("Unexpected response received from HTTP on port {0}.", port));

                    if (!listenTask.Wait(5000))
                        throw new Exception("PortsHelper: time out waiting for HttpListener to return.");
                    if (httpListenerError != null)
                        throw httpListenerError;

                    httpListener.Stop();
                }
                catch (Exception exc)
                {
                    Log.TraceException(exc, "PortsHelper: port {0} unavailable for HttpListener. Error: {1}.", port, exc.Message);
                    continue;
                }

                AvailablePorts.Enqueue(port);
                succ += 1;
            }

            Log.Trace("PortsHelper: {0} ports are available at [{1}].", succ, ip);
            if (succ <= PortCount/2)
                throw new Exception("More than half requested ports are unavailable.");

            Log.Trace("PortsHelper: test took {0}.", sw.Elapsed);
        }

        public static int GetAvailablePort(IPAddress ip)
        {
            for (int i = 0; i < 50; ++i)
            {
                int port;
                if (!AvailablePorts.TryDequeue(out port))
                    throw new Exception("Couldn't get free TCP port for MiniNode.");

/*
                try
                {
                    var listener = new TcpListener(ip, port);
                    listener.Start();
                    listener.Stop();
                }
                catch (Exception)
                {
                    AvailablePorts.Enqueue(port);
                    continue;
                }
*/

                try
                {
                    var httpListener = new HttpListener();
					httpListener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", port));
                    httpListener.Start();
                    httpListener.Stop();
                }
                catch (Exception)
                {
                    AvailablePorts.Enqueue(port);
                    continue;
//                    throw new Exception(
//                        string.Format("HttpListener couldn't listen on port {0}, but TcpListener was OK.\nError: {1}", port, exc), exc);
                }
                return port;
            }
            throw new Exception("Reached trials limit while trying to get free port for MiniNode");
        }

        public static void ReturnPort(int port)
        {
            if(!AvailablePorts.Contains(port))
                AvailablePorts.Enqueue(port);
        }

/*
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
*/
    }
}
