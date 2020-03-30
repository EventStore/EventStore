namespace EventStore.Plugins.Authorization {
	public static class Operations {
		public static class Node {
			private const string Resource = "node";
			public static readonly OperationDefinition Redirect = new Operation(Resource, "redirect");
			public static readonly OperationDefinition Options = new Operation(Resource, "options");
			public static readonly OperationDefinition Ping = new Operation(Resource, "ping");

			public static readonly OperationDefinition StaticContent =
				new OperationDefinition(Resource + "/content", "read");

			public static readonly OperationDefinition Shutdown = new OperationDefinition(Resource, "shutdown");
			public static readonly OperationDefinition MergeIndexes = new OperationDefinition(Resource, "mergeIndexes");
			public static readonly OperationDefinition SetPriority = new OperationDefinition(Resource, "setPriority");
			public static readonly OperationDefinition Resign = new OperationDefinition(Resource, "resign");

			public static class Scavenge {
				private const string Resource = Node.Resource + "/scavenge";
				public static readonly OperationDefinition Start = new OperationDefinition(Resource, "start");
				public static readonly OperationDefinition Stop = new OperationDefinition(Resource, "stop");
			}


			public static class Information {
				private const string Resource = Node.Resource + "/info";

				public static readonly OperationDefinition Subsystems =
					new OperationDefinition(Resource + "/subsystems", "read");

				public static readonly OperationDefinition Histogram =
					new OperationDefinition(Resource + "/histograms", "read");

				public static readonly OperationDefinition Read = new OperationDefinition(Resource, "read");

				public static readonly OperationDefinition Options =
					new OperationDefinition(Resource + "/options", "read");
			}

			public static class Statistics {
				private const string Resource = Node.Resource + "/stats";
				public static readonly OperationDefinition Read = new OperationDefinition(Resource, "read");

				public static readonly OperationDefinition Replication =
					new OperationDefinition(Resource + "/replication", "read");

				public static readonly OperationDefinition Tcp = new OperationDefinition(Resource + "/tcp", "read");

				public static readonly OperationDefinition Custom =
					new OperationDefinition(Resource + "/custom", "read");
			}

			public static class Elections {
				private const string Resource = Node.Resource + "/elections";
				public static readonly OperationDefinition ViewChange = new OperationDefinition(Resource, "viewchange");

				public static readonly OperationDefinition ViewChangeProof =
					new OperationDefinition(Resource, "viewchangeproof");

				public static readonly OperationDefinition Prepare = new OperationDefinition(Resource, "prepare");
				public static readonly OperationDefinition PrepareOk = new OperationDefinition(Resource, "prepareOk");
				public static readonly OperationDefinition Proposal = new OperationDefinition(Resource, "proposal");
				public static readonly OperationDefinition Accept = new OperationDefinition(Resource, "accept");

				public static readonly OperationDefinition LeaderIsResigning =
					new OperationDefinition(Resource, "leaderisresigning");

				public static readonly OperationDefinition LeaderIsResigningOk =
					new OperationDefinition(Resource, "leaderisresigningok");
			}

			public static class Gossip {
				private const string Resource = Node.Resource + "/gossip";
				public static readonly OperationDefinition Read = new OperationDefinition(Resource, "read");
				public static readonly OperationDefinition Update = new OperationDefinition(Resource, "update");
			}
		}

		public static class Streams {
			private const string Resource = "streams";
			public static readonly OperationDefinition Read = new OperationDefinition(Resource, "read");
			public static readonly OperationDefinition Write = new OperationDefinition(Resource, "write");
			public static readonly OperationDefinition Delete = new OperationDefinition(Resource, "delete");
			public static readonly OperationDefinition MetadataRead = new OperationDefinition(Resource, "metadataRead");

			public static readonly OperationDefinition MetadataWrite =
				new OperationDefinition(Resource, "metadataWrite");

			public static class Parameters {
				public static Parameter StreamId(string streamId) => new Parameter("streamId", streamId);

				public static Parameter TransactionId(long transactionId) =>
					new Parameter("transactionId", transactionId.ToString("D"));
			}
		}

		public static class Subscriptions {
			private const string Resource = "subscriptions";
			public static readonly OperationDefinition Statistics = new OperationDefinition(Resource, "statistics");
			public static readonly OperationDefinition Create = new OperationDefinition(Resource, "create");
			public static readonly OperationDefinition Update = new OperationDefinition(Resource, "update");
			public static readonly OperationDefinition Delete = new OperationDefinition(Resource, "delete");
			public static readonly OperationDefinition ReplayParked = new OperationDefinition(Resource, "replay");

			public static readonly OperationDefinition ProcessMessages = new OperationDefinition(Resource, "process");


			public static class Parameters {
				public static Parameter SubscriptionId(string id) => new Parameter("subscriptionId", id);
				public static Parameter StreamId(string streamId) => new Parameter("streamId", streamId);
			}
		}

		public static class Users {
			private const string Resource = "users";
			public static readonly OperationDefinition Create = new OperationDefinition(Resource, "create");
			public static readonly OperationDefinition Update = new OperationDefinition(Resource, "update");
			public static readonly OperationDefinition Delete = new OperationDefinition(Resource, "delete");
			public static readonly OperationDefinition List = new OperationDefinition(Resource, "list");
			public static readonly OperationDefinition Read = new OperationDefinition(Resource, "read");
			public static readonly OperationDefinition CurrentUser = new OperationDefinition(Resource, "self");
			public static readonly OperationDefinition Enable = new OperationDefinition(Resource, "enable");
			public static readonly OperationDefinition Disable = new OperationDefinition(Resource, "disable");

			public static readonly OperationDefinition ResetPassword =
				new OperationDefinition(Resource, "resetPassword");

			public static readonly OperationDefinition ChangePassword =
				new OperationDefinition(Resource, "updatePassword");

			public static class Parameters {
				public const string UserParameterName = "user";

				public static Parameter User(string userId) {
					return new Parameter(UserParameterName, userId);
				}
			}
		}

		public static class Projections {
			private const string Resource = "projections";

			public static readonly OperationDefinition Create = new OperationDefinition(Resource, "create");
			public static readonly OperationDefinition Update = new OperationDefinition(Resource, "update");
			public static readonly OperationDefinition Read = new OperationDefinition(Resource, "read");

			public static readonly OperationDefinition Abort = new OperationDefinition(Resource, "abort");

			public static readonly OperationDefinition List = new OperationDefinition(Resource, "list");
			public static readonly OperationDefinition Restart = new OperationDefinition(Resource, "restart");
			public static readonly OperationDefinition Delete = new OperationDefinition(Resource, "delete");
			public static readonly OperationDefinition Enable = new OperationDefinition(Resource, "enable");
			public static readonly OperationDefinition Disable = new OperationDefinition(Resource, "disable");
			public static readonly OperationDefinition Reset = new OperationDefinition(Resource, "reset");

			public static readonly OperationDefinition ReadConfiguration =
				new OperationDefinition(Resource + "/configuration", "read");

			public static readonly OperationDefinition UpdateConfiguration =
				new OperationDefinition(Resource + "/configuration", "update");

			public static readonly OperationDefinition Status = new OperationDefinition(Resource, "status");

			//can be Partition based
			public static readonly OperationDefinition State = new OperationDefinition(Resource, "state");
			public static readonly OperationDefinition Result = new OperationDefinition(Resource, "state");

			public static readonly OperationDefinition Statistics = new OperationDefinition(Resource, "statistics");

			//This one is a bit weird
			public static readonly OperationDefinition DebugProjection = new OperationDefinition(Resource, "debug");

			public static class Parameters {
				public static readonly Parameter Query = new Parameter("projectionType", "query");
				public static readonly Parameter OneTime = new Parameter("projectionType", "onetime");
				public static readonly Parameter Continuous = new Parameter("projectionType", "continuous");

				public static Parameter Projection(string name) {
					return new Parameter("projection", name);
				}

				public static Parameter RunAs(string name) {
					return new Parameter("runas", name);
				}
			}
		}
	}
}
