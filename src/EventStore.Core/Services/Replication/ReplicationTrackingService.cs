using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services.Replication {

	public class ReplicationTrackingService :
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<SystemMessage.SystemInit>,
		IHandle<ReplicationTrackingMessage.ReplicaWriteAck>,
		IHandle<ReplicationTrackingMessage.WriterCheckpointFlushed>,
		IHandle<ReplicationTrackingMessage.MasterReplicatedTo> {
		private readonly ILogger _log = LogManager.GetLoggerFor<ReplicationTrackingService>();
		private readonly IPublisher _publisher;
		private readonly ICheckpoint _replicationCheckpoint;
		private readonly ICheckpoint _writerCheckpoint;
		private readonly int _quorumSize;
		private Thread _thread;
		private bool _stop;
		private VNodeState _state;
		private long _idle = 1;
		private long _publishedPosition;
		private readonly ConcurrentDictionary<Guid, long> _replicaLogPositions = new ConcurrentDictionary<Guid, long>();

		private readonly ManualResetEventSlim _replicationChange = new ManualResetEventSlim(false, 1);
		private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();

		public Task Task {
			get { return _tcs.Task; }
		}

		public ReplicationTrackingService(
			IPublisher publisher,
			int clusterNodeCount,
			ICheckpoint replicationCheckpoint,
			ICheckpoint writerCheckpoint) {
			Ensure.NotNull(publisher, nameof(publisher));
			Ensure.NotNull(replicationCheckpoint, nameof(replicationCheckpoint));
			Ensure.NotNull(writerCheckpoint, nameof(writerCheckpoint));
			Ensure.Positive(clusterNodeCount, nameof(clusterNodeCount));
			_publisher = publisher;
			_replicationCheckpoint = replicationCheckpoint;
			_writerCheckpoint = writerCheckpoint;
			_quorumSize = clusterNodeCount / 2 + 1;
		}

		public void Start() {
			_thread = new Thread(TrackReplication) { IsBackground = true, Name = nameof(ReplicationTrackingService) };
			_thread.Start();
		}

		public void Stop() {
			_stop = true;
		}
		public bool IsIdle() { return Interlocked.Read(ref _idle) == 1; }
		public bool IsCurrent() { return Interlocked.Read(ref _publishedPosition) == _replicationCheckpoint.Read(); }
		private void TrackReplication() {

			try {
				while (!_stop) {
					_replicationChange.Reset();
					if (_state == VNodeState.Master) {
						//Publish Log Commit Position
						var newPos = _replicationCheckpoint.Read();
						if (newPos > Interlocked.Read(ref _publishedPosition)) {
							_publisher.Publish(new ReplicationTrackingMessage.ReplicatedTo(newPos));
							Interlocked.Exchange(ref _publishedPosition, newPos);
						}
					}
					_idle = 1;
					_replicationChange.Wait(100);
					_idle = 0;
				}
			} catch (Exception exc) {
				_log.FatalException(exc, $"Error in {nameof(ReplicationTrackingService)}. Terminating...");
				_tcs.TrySetException(exc);
				Application.Exit(ExitCode.Error,
					$"Error in {nameof(ReplicationTrackingService)}. Terminating...\nError: " + exc.Message);
				//TODO(clc): is this right, are we waiting for someone to clean us up???
				while (!_stop) {
					Thread.Sleep(100);
				}
			}
			_publisher.Publish(new SystemMessage.ServiceShutdown(nameof(ReplicationTrackingService)));
		}

		public void Handle(ReplicationTrackingMessage.MasterReplicatedTo message) {
			if (_state != VNodeState.Master && message.LogPosition > _replicationCheckpoint.Read()) {
				_replicationCheckpoint.Write(message.LogPosition);
				_replicationCheckpoint.Flush();
				_publisher.Publish(new ReplicationTrackingMessage.ReplicatedTo(message.LogPosition));
			}
		}


		private void UpdateReplicationPosition() {

			var replicationCp = _replicationCheckpoint.Read();
			var writerCp = _writerCheckpoint.Read();
			if (writerCp <= replicationCp) { return; }

			var minReplicas = _quorumSize - 1; //total - master = min replicas
			if (minReplicas == 0) {
				_replicationCheckpoint.Write(writerCp);
				_replicationCheckpoint.Flush();
				_replicationChange.Set();
				return;
			}
			long[] positions;
			lock (_replicaLogPositions) {
				positions = _replicaLogPositions.Values.ToArray();
			}

			if (positions.Length < minReplicas) { return; }

			Array.Sort(positions);
			var furthestReplicatedPosition = positions[minReplicas - 1];
			if (furthestReplicatedPosition <= replicationCp) { return; }

			var newReplicationPoint = Math.Min(writerCp, furthestReplicatedPosition);
			_replicationCheckpoint.Write(newReplicationPoint);
			_replicationCheckpoint.Flush();
			_replicationChange.Set();
		}


		public void Handle(ReplicationTrackingMessage.ReplicaWriteAck message) {
			if (_state != VNodeState.Master) { return; }
			if (_replicaLogPositions.TryGetValue(message.SubscriptionId, out var position) &&
				message.ReplicationLogPosition <= position) { return; }
			_replicaLogPositions.AddOrUpdate(message.SubscriptionId, message.ReplicationLogPosition, (k, v) => message.ReplicationLogPosition);
			UpdateReplicationPosition();
		}

		public void Handle(ReplicationTrackingMessage.WriterCheckpointFlushed message) {
			if (_state != VNodeState.Master) { return; }
			UpdateReplicationPosition();
		}

		public void Handle(SystemMessage.StateChangeMessage msg) {
			//switching to master from non-Master
			if (_state != msg.State) {
				_replicaLogPositions.Clear();
			}
			_state = msg.State;
		}

		public void Handle(SystemMessage.BecomeShuttingDown message) {
			Stop();
		}

		public void Handle(SystemMessage.SystemInit message) {
			Start();
		}


	}
}
