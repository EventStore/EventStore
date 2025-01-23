// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Client;
using Grpc.Core;

namespace EventStore.Cluster {
	partial class Gossip {
		partial class GossipBase : ServiceBase {

		}
	}

	partial class Elections {
		partial class ElectionsBase : ServiceBase {

		}
	}
}

namespace EventStore.Client.PersistentSubscriptions {
	partial class PersistentSubscriptions {
		partial class PersistentSubscriptionsBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Streams {
	partial class Streams {
		partial class StreamsBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Users {
	partial class Users {
		partial class UsersBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Operations {
	partial class Operations {
		partial class OperationsBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Gossip {
	partial class Gossip {
		partial class GossipBase : ServiceBase {
		}
	}
}

namespace EventStore.Client.Monitoring {
	partial class Monitoring {
		partial class MonitoringBase : ServiceBase {
		}
	}
}

namespace EventStore.Core.Services.Transport.Grpc {
	public class ServiceBase {
		protected static bool GetRequiresLeader(Metadata requestHeaders) {
			var requiresLeaderHeaderValue =
				requestHeaders.FirstOrDefault(x => x.Key == Constants.Headers.RequiresLeader)?.Value;
			if (string.IsNullOrEmpty(requiresLeaderHeaderValue)) return false;
			bool.TryParse(requiresLeaderHeaderValue, out var requiresLeader);
			return requiresLeader;
		}
	}
}
