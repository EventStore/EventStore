using System;
using System.Linq;
using System.Net;
using EventStore.Common.Log;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace EventStore.Core.Tests.Helpers {
	public static class PortsHelper {
		private static readonly ILogger Log = LogManager.GetLogger("PortsHelper");

		public const int PortStart = 49152;
		public const int PortCount = ushort.MaxValue - PortStart;

		private static readonly ConcurrentQueue<int> AvailablePorts =
			new ConcurrentQueue<int>(Enumerable.Range(PortStart, PortCount));

		public static int GetAvailablePort(IPAddress ip) {
			const int maxAttempts = 50;
			var properties = IPGlobalProperties.GetIPGlobalProperties();

			var ipEndPoints = properties.GetActiveTcpConnections().Select(x => x.LocalEndPoint)
				.Concat(properties.GetActiveTcpListeners())
				.Concat(properties.GetActiveUdpListeners())
				.Where(x => x.AddressFamily == AddressFamily.InterNetwork &&
							x.Address.Equals(ip) &&
							x.Port >= PortStart &&
							x.Port < PortStart + PortCount)
				.OrderBy(x => x.Port)
				.ToArray();
			var inUse = new HashSet<int>(ipEndPoints.Select(x => x.Port));

			var attempt = 0;

			var isOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

			while (attempt++ < maxAttempts && AvailablePorts.TryDequeue(out var port)) {
				if (!inUse.Contains(port)) {
					if (isOsx) {
						var endpoint = new IPEndPoint(ip, port);
						try {
							using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
								ProtocolType.Tcp)) {
								socket.Bind(endpoint);
								return port;
							}
						} catch (Exception ex) {
							Log.Warn(
								$"Could not bind to {endpoint} even though {nameof(IPGlobalProperties.GetActiveTcpConnections)} said it was free: {ex}");
						}
					} else {
						return port;
					}
				}

				AvailablePorts.Enqueue(port);
			}

			throw new Exception(
				$"Could not find free port on {ip} after {attempt} attempts. The following ports are used: {string.Join(",", inUse)}");
		}

		public static void ReturnPort(int port) => AvailablePorts.Enqueue(port);
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
