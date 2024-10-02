// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common.Utils;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class LeaderDiscoveryMessage {
		[DerivedMessage(CoreMessage.LeaderDiscovery)]
		public partial class LeaderFound : Message {
			public readonly MemberInfo Leader;

			public LeaderFound(MemberInfo leader) {
				Ensure.NotNull(leader, "leader");
				Leader = leader;
			}
		}

		[DerivedMessage(CoreMessage.LeaderDiscovery)]
		public partial class DiscoveryTimeout : Message {
		}
	}
}
