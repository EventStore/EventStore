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
