using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Tcp;

namespace EventStore.Core.Services.Replication {
	public class ReplicaService : IHandle<SystemMessage.StateChangeMessage>,
		IHandle<ReplicationMessage.ReconnectToMaster>,
		IHandle<ReplicationMessage.SubscribeToMaster>,
		IHandle<ReplicationMessage.AckLogPosition>,
		IHandle<StorageMessage.PrepareAck>,
		IHandle<StorageMessage.CommitAck>,
		IHandle<ClientMessage.TcpForwardMessage> {
		private static readonly ILogger Log = LogManager.GetLoggerFor<ReplicaService>();

		private readonly TcpClientConnector _connector;
		private readonly IPublisher _publisher;
		private readonly TFChunkDb _db;
		private readonly IEpochManager _epochManager;
		private readonly IPublisher _networkSendQueue;
		private readonly IAuthenticationProvider _authProvider;

		private readonly VNodeInfo _nodeInfo;
		private readonly bool _useSsl;
		private readonly string _sslTargetHost;
		private readonly bool _sslValidateServer;
		private readonly TimeSpan _heartbeatTimeout;
		private readonly TimeSpan _heartbeatInterval;

		private readonly InternalTcpDispatcher _tcpDispatcher = new InternalTcpDispatcher();

		private VNodeState _state = VNodeState.Initializing;
		private TcpConnectionManager _connection;

		public ReplicaService(IPublisher publisher,
			TFChunkDb db,
			IEpochManager epochManager,
			IPublisher networkSendQueue,
			IAuthenticationProvider authProvider,
			VNodeInfo nodeInfo,
			bool useSsl,
			string sslTargetHost,
			bool sslValidateServer,
			TimeSpan heartbeatTimeout,
			TimeSpan heartbeatInterval) {
			Ensure.NotNull(publisher, "publisher");
			Ensure.NotNull(db, "db");
			Ensure.NotNull(epochManager, "epochManager");
			Ensure.NotNull(networkSendQueue, "networkSendQueue");
			Ensure.NotNull(authProvider, "authProvider");
			Ensure.NotNull(nodeInfo, "nodeInfo");
			if (useSsl) Ensure.NotNull(sslTargetHost, "sslTargetHost");

			_publisher = publisher;
			_db = db;
			_epochManager = epochManager;
			_networkSendQueue = networkSendQueue;
			_authProvider = authProvider;

			_nodeInfo = nodeInfo;
			_useSsl = useSsl;
			_sslTargetHost = sslTargetHost;
			_sslValidateServer = sslValidateServer;
			_heartbeatTimeout = heartbeatTimeout;
			_heartbeatInterval = heartbeatInterval;

			_connector = new TcpClientConnector();
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			_state = message.State;

			switch (message.State) {
				case VNodeState.Initializing:
				case VNodeState.Unknown:
				case VNodeState.PreMaster:
				case VNodeState.Master:
				case VNodeState.ShuttingDown:
				case VNodeState.Shutdown: {
					Disconnect();
					break;
				}
				case VNodeState.PreReplica: {
					var m = (SystemMessage.BecomePreReplica)message;
					ConnectToMaster(m.Master);
					break;
				}
				case VNodeState.CatchingUp:
				case VNodeState.Clone:
				case VNodeState.Slave: {
					// nothing changed, essentially
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void Disconnect() {
			if (_connection != null) {
				_connection.Stop(string.Format("Node state changed to {0}. Closing replication connection.", _state));
				_connection = null;
			}
		}

		private void OnConnectionEstablished(TcpConnectionManager manager) {
			_publisher.Publish(
				new SystemMessage.VNodeConnectionEstablished(manager.RemoteEndPoint, manager.ConnectionId));
		}

		private void OnConnectionClosed(TcpConnectionManager manager, SocketError socketError) {
			_publisher.Publish(new SystemMessage.VNodeConnectionLost(manager.RemoteEndPoint, manager.ConnectionId));
		}

		public void Handle(ReplicationMessage.ReconnectToMaster message) {
			ConnectToMaster(message.Master);
		}

		private void ConnectToMaster(VNodeInfo master) {
			Debug.Assert(_state == VNodeState.PreReplica);

			var masterEndPoint = GetMasterEndPoint(master, _useSsl);

			if (_connection != null)
				_connection.Stop(string.Format("Reconnecting from old master [{0}] to new master: [{1}].",
					_connection.RemoteEndPoint, masterEndPoint));

			_connection = new TcpConnectionManager(_useSsl ? "master-secure" : "master-normal",
				Guid.NewGuid(),
				_tcpDispatcher,
				_publisher,
				masterEndPoint,
				_connector,
				_useSsl,
				_sslTargetHost,
				_sslValidateServer,
				_networkSendQueue,
				_authProvider,
				_heartbeatInterval,
				_heartbeatTimeout,
				OnConnectionEstablished,
				OnConnectionClosed);
			_connection.StartReceiving();
		}

		private static IPEndPoint GetMasterEndPoint(VNodeInfo master, bool useSsl) {
			Ensure.NotNull(master, "master");
			if (useSsl && master.InternalSecureTcp == null)
				Log.Error(
					"Internal secure connections are required, but no internal secure TCP end point is specified for master [{master}]!",
					master);
			return useSsl ? master.InternalSecureTcp ?? master.InternalTcp : master.InternalTcp;
		}

		public void Handle(ReplicationMessage.SubscribeToMaster message) {
			if (_state != VNodeState.PreReplica)
				throw new Exception(string.Format("_state is {0}, but is expected to be {1}", _state,
					VNodeState.PreReplica));

			var logPosition = _db.Config.WriterCheckpoint.ReadNonFlushed();
			var epochs = _epochManager.GetLastEpochs(ClusterConsts.SubscriptionLastEpochCount).ToArray();

			Log.Info(
				"Subscribing at LogPosition: {logPosition} (0x{logPosition:X}) to MASTER [{remoteEndPoint}, {masterId:B}] as replica with SubscriptionId: {subscriptionId:B}, "
				+ "ConnectionId: {connectionId:B}, LocalEndPoint: [{localEndPoint}], Epochs:\n{epochs}...\n.",
				logPosition, logPosition, _connection.RemoteEndPoint, message.MasterId, message.SubscriptionId,
				_connection.ConnectionId, _connection.LocalEndPoint,
				string.Join("\n", epochs.Select(x => x.AsString())));

			var chunk = _db.Manager.GetChunkFor(logPosition);
			if (chunk == null)
				throw new Exception(string.Format("Chunk was null during subscribing at {0} (0x{0:X}).", logPosition));
			SendTcpMessage(_connection,
				new ReplicationMessage.SubscribeReplica(
					logPosition, chunk.ChunkHeader.ChunkId, epochs, _nodeInfo.InternalTcp,
					message.MasterId, message.SubscriptionId, isPromotable: true));
		}

		public void Handle(ReplicationMessage.AckLogPosition message) {
			if (!_state.IsReplica()) throw new Exception("!_state.IsReplica()");
			if (_connection == null) throw new Exception("_connection == null");
			SendTcpMessage(_connection, message);
		}

		public void Handle(StorageMessage.PrepareAck message) {
			if (_state == VNodeState.Slave) {
				Debug.Assert(_connection != null, "_connection == null");
				SendTcpMessage(_connection, message);
			}
		}

		public void Handle(StorageMessage.CommitAck message) {
			if (_state == VNodeState.Slave) {
				Debug.Assert(_connection != null, "_connection == null");
				SendTcpMessage(_connection, message);
			}
		}

		public void Handle(ClientMessage.TcpForwardMessage message) {
			switch (_state) {
				case VNodeState.PreReplica: {
					if (_connection != null)
						SendTcpMessage(_connection, message.Message);
					break;
				}

				case VNodeState.CatchingUp:
				case VNodeState.Clone:
				case VNodeState.Slave: {
					Debug.Assert(_connection != null, "Connection manager is null in slave/clone/catching up state");
					SendTcpMessage(_connection, message.Message);
					break;
				}

				default:
					throw new Exception(string.Format("Unexpected state: {0}", _state));
			}
		}

		private void SendTcpMessage(TcpConnectionManager manager, Message msg) {
			_networkSendQueue.Publish(new TcpMessage.TcpSend(manager, msg));
		}
	}
}
