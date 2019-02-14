using System;
using System.Threading;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages {
	public static class CoreProjectionManagementMessage {
		public class Start : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Start(Guid projectionId, Guid workerId)
				: base(projectionId, workerId) {
			}
		}

		public class LoadStopped : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public LoadStopped(Guid correlationId, Guid workerId)
				: base(correlationId, workerId) {
			}
		}

		public class Stop : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Stop(Guid projectionId, Guid workerId)
				: base(projectionId, workerId) {
			}
		}

		public class Kill : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Kill(Guid projectionId, Guid workerId)
				: base(projectionId, workerId) {
			}
		}

		public class GetState : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly Guid _correlationId;
			private readonly string _partition;

			public GetState(Guid correlationId, Guid projectionId, string partition, Guid workerId)
				: base(projectionId, workerId) {
				if (partition == null) throw new ArgumentNullException("partition");
				_correlationId = correlationId;
				_partition = partition;
			}

			public string Partition {
				get { return _partition; }
			}

			public Guid CorrelationId {
				get { return _correlationId; }
			}
		}

		public class GetResult : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly Guid _correlationId;
			private readonly string _partition;

			public GetResult(Guid correlationId, Guid projectionId, string partition, Guid workerId)
				: base(projectionId, workerId) {
				if (partition == null) throw new ArgumentNullException("partition");
				_correlationId = correlationId;
				_partition = partition;
			}

			public string Partition {
				get { return _partition; }
			}

			public Guid CorrelationId {
				get { return _correlationId; }
			}
		}

		public class CreateAndPrepare : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly ProjectionConfig _config;
			private readonly string _handlerType;
			private readonly string _query;
			private readonly string _name;
			private readonly ProjectionVersion _version;

			public CreateAndPrepare(
				Guid projectionId,
				Guid workerId,
				string name,
				ProjectionVersion version,
				ProjectionConfig config,
				string handlerType,
				string query)
				: base(projectionId, workerId) {
				_name = name;
				_version = version;
				_config = config;
				_handlerType = handlerType;
				_query = query;
			}

			public ProjectionConfig Config {
				get { return _config; }
			}

			public string Name {
				get { return _name; }
			}

			public ProjectionVersion Version {
				get { return _version; }
			}

			public string HandlerType {
				get { return _handlerType; }
			}

			public string Query {
				get { return _query; }
			}
		}

		public class CreateAndPrepareSlave : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly Guid _masterCoreProjectionId;
			private readonly ProjectionConfig _config;
			private readonly Guid _masterWorkerId;
			private readonly string _handlerType;
			private readonly string _query;
			private readonly string _name;
			private readonly ProjectionVersion _version;

			public CreateAndPrepareSlave(
				Guid projectionId,
				Guid workerId,
				string name,
				ProjectionVersion version,
				ProjectionConfig config,
				Guid masterMasterWorkerId,
				Guid masterCoreProjectionId,
				string handlerType,
				string query)
				: base(projectionId, workerId) {
				if (name == null) throw new ArgumentNullException("name");
				if (config == null) throw new ArgumentNullException("config");
				if (handlerType == null) throw new ArgumentNullException("handlerType");
				if (query == null) throw new ArgumentNullException("query");
				if (masterMasterWorkerId == Guid.Empty)
					throw new ArgumentException("Must not be empty", "masterMasterWorkerId");
				_name = name;
				_version = version;
				_config = config;
				_masterWorkerId = masterMasterWorkerId;
				_masterCoreProjectionId = masterCoreProjectionId;
				_handlerType = handlerType;
				_query = query;
			}

			public ProjectionConfig Config {
				get { return _config; }
			}

			public string Name {
				get { return _name; }
			}

			public ProjectionVersion Version {
				get { return _version; }
			}

			public Guid MasterCoreProjectionId {
				get { return _masterCoreProjectionId; }
			}

			public string HandlerType {
				get { return _handlerType; }
			}

			public string Query {
				get { return _query; }
			}

			public Guid MasterWorkerId {
				get { return _masterWorkerId; }
			}
		}

		public class CreatePrepared : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly ProjectionConfig _config;
			private readonly QuerySourcesDefinition _sourceDefinition;
			private readonly string _handlerType;
			private readonly string _query;
			private readonly string _name;
			private readonly ProjectionVersion _version;

			public CreatePrepared(
				Guid projectionId,
				Guid workerId,
				string name,
				ProjectionVersion version,
				ProjectionConfig config,
				QuerySourcesDefinition sourceDefinition,
				string handlerType,
				string query)
				: base(projectionId, workerId) {
				if (name == null) throw new ArgumentNullException("name");
				if (config == null) throw new ArgumentNullException("config");
				if (sourceDefinition == null) throw new ArgumentNullException("sourceDefinition");
				if (handlerType == null) throw new ArgumentNullException("handlerType");
				if (query == null) throw new ArgumentNullException("query");
				_name = name;
				_version = version;
				_config = config;
				_sourceDefinition = sourceDefinition;
				_handlerType = handlerType;
				_query = query;
			}

			public ProjectionConfig Config {
				get { return _config; }
			}

			public string Name {
				get { return _name; }
			}

			public QuerySourcesDefinition SourceDefinition {
				get { return _sourceDefinition; }
			}

			public ProjectionVersion Version {
				get { return _version; }
			}

			public string HandlerType {
				get { return _handlerType; }
			}

			public string Query {
				get { return _query; }
			}
		}

		public class Dispose : CoreProjectionManagementControlMessage {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Dispose(Guid projectionId, Guid workerId)
				: base(projectionId, workerId) {
			}
		}

		public sealed class
			SlaveProjectionReaderAssigned : CoreProjectionStatusMessage.CoreProjectionStatusMessageBase {
			private readonly Guid _subscriptionId;
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public SlaveProjectionReaderAssigned(Guid projectionId, Guid subscriptionId)
				: base(projectionId) {
				_subscriptionId = subscriptionId;
			}

			public Guid SubscriptionId {
				get { return _subscriptionId; }
			}
		}
	}
}
