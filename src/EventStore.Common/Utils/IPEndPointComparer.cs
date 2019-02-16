using System.Collections.Generic;
using System.Net;

namespace EventStore.Common.Utils {
	public class IPEndPointComparer : IComparer<IPEndPoint> {
		public int Compare(IPEndPoint x, IPEndPoint y) {
			var xx = x.Address.ToString();
			var yy = y.Address.ToString();
			var result = string.CompareOrdinal(xx, yy);
			return result == 0 ? x.Port.CompareTo(y.Port) : result;
		}
	}
}
