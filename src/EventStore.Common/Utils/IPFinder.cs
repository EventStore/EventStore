// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net;
using System.Net.NetworkInformation;

namespace EventStore.Common.Utils {
	public static class IPFinder {
		public static IPAddress GetNonLoopbackAddress() {
			foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
				foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses) {
					if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
						if (!IPAddress.IsLoopback(address.Address)) {
							return address.Address;
						}
					}
				}
			}

			return null;
		}
	}
}
