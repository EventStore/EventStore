using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Messages;

namespace EventStore.Core.Cluster {
	public class ClusterInfo {
		private static readonly IPEndPointComparer Comparer = new IPEndPointComparer();

		public readonly MemberInfo[] Members;

		public ClusterInfo(params MemberInfo[] members) : this((IEnumerable<MemberInfo>)members) {
		}

		public ClusterInfo(IEnumerable<MemberInfo> members) {
			Members = members.Safe().OrderByDescending<MemberInfo, IPEndPoint>(x => x.InternalHttpEndPoint, Comparer)
				.ToArray();
		}

		public ClusterInfo(ClusterInfoDto dto) {
			Members = dto.Members.Safe().Select(x => new MemberInfo(x))
				.OrderByDescending<MemberInfo, IPEndPoint>(x => x.InternalHttpEndPoint, Comparer).ToArray();
		}

		public override string ToString() {
			return string.Join("\n", Members.Select(s => s.ToString()));
		}

		public bool HasChangedSince(ClusterInfo other) {
			if (ReferenceEquals(null, other)) return true;
			if (ReferenceEquals(this, other)) return false;

			if (other.Members.Length != Members.Length)
				return true;

			for (int i = 0; i < Members.Length; i++) {
				if (!Members[i].Equals(other.Members[i]))
					return true;
			}

			return false;
		}
	}
}
