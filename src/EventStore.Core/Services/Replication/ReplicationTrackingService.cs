using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Checkpoint;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Replication {

	public class ReplicationTrackingService :
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<SystemMessage.SystemInit>,
		IHandle<ReplicationTrackingMessage.ReplicaWriteAck>,
		IHandle<ReplicationTrackingMessage.WriterCheckpointFlushed>,
		IHandle<ReplicationTrackingMessage.LeaderReplicatedTo>,
		IHandle<SystemMessage.VNodeConnectionLost> {
		private readonly ILogger _log = Serilog.Log.ForContext<ReplicationTrackingService>();
		private readonly IPublisher _publisher;
		private readonly ICheckpoint _replicationCheckpoint;
		private readonly IReadOnlyCheckpoint _writerCheckpoint;
		private readonly int _quorumSize;
		private Thread _thread;
		private bool _stop;
		private VNodeState _state;
		private long _publishedPosition;
		private DateTime _lastReplicationCheckpointFlush = DateTime.MinValue;
		private readonly TimeSpan _replicationCheckpointFlushInterval = TimeSpan.FromMilliseconds(2);

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
			IReadOnlyCheckpoint writerCheckpoint) {
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

		public bool IsCurrent() {
			Debug.Assert(_state == VNodeState.Leader);
			return Interlocked.Read(ref _publishedPosition) == _replicationCheckpoint.ReadNonFlushed();
		}

		private void UpdateReplicationCheckpoint(long value) {
			_replicationCheckpoint.Write(value);
			var now = DateTime.UtcNow;
			if (now - _lastReplicationCheckpointFlush >= _replicationCheckpointFlushInterval) {
				_replicationCheckpoint.Flush();
				_lastReplicationCheckpointFlush = now;
			}
			_replicationChange.Set();
		}

		private void TrackReplication() {

			try {
				while (!_stop) {
					_replicationChange.Reset();
					if (_state == VNodeState.Leader) {
						//Publish Log Commit Position
						var newPos = _replicationCheckpoint.ReadNonFlushed();
						if (newPos > Interlocked.Read(ref _publishedPosition)) {
							_publisher.Publish(new ReplicationTrackingMessage.ReplicatedTo(newPos));
							Interlocked.Exchange(ref _publishedPosition, newPos);
						}
					}
					_replicationChange.Wait(100);
				}
			} catch (Exception exc) {
				_log.Fatal(exc, $"Error in {nameof(ReplicationTrackingService)}. Terminating...");
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

		public void Handle(ReplicationTrackingMessage.LeaderReplicatedTo message) {
			if (_state != VNodeState.Leader && message.LogPosition > _replicationCheckpoint.ReadNonFlushed()) {
				UpdateReplicationCheckpoint(message.LogPosition);
				_publisher.Publish(new ReplicationTrackingMessage.ReplicatedTo(message.LogPosition));
			}
		}


		private void UpdateReplicationPosition() {
			var replicationCp = _replicationCheckpoint.ReadNonFlushed();
			var writerCp = _writerCheckpoint.Read();
			if (writerCp <= replicationCp) { return; }

			var minReplicas = _quorumSize - 1; //total - leader = min replicas
			if (minReplicas == 0) {
				UpdateReplicationCheckpoint(writerCp);
				return;
			}
			long[] positions;
			lock (_replicaLogPositions) {
				positions = _replicaLogPositions.Values.ToArray();
			}

			if (positions.Length < minReplicas) { return; }

			Array.Sort(positions);
			var furthestReplicatedPosition = positions[^minReplicas];
			if (furthestReplicatedPosition <= replicationCp) { return; }

			var newReplicationPoint = Math.Min(writerCp, furthestReplicatedPosition);
			UpdateReplicationCheckpoint(newReplicationPoint);
		}


		public void Handle(ReplicationTrackingMessage.ReplicaWriteAck message) {
			if (_state != VNodeState.Leader) { return; }
			if (_replicaLogPositions.TryGetValue(message.SubscriptionId, out var position) &&
				message.ReplicationLogPosition <= position) { return; }
			_replicaLogPositions.AddOrUpdate(message.SubscriptionId, message.ReplicationLogPosition, (k, v) => message.ReplicationLogPosition);
			UpdateReplicationPosition();
		}

		public void Handle(ReplicationTrackingMessage.WriterCheckpointFlushed message) {
			if (_state != VNodeState.Leader) { return; }
			UpdateReplicationPosition();
		}

		public void Handle(SystemMessage.StateChangeMessage msg) {
			//switching to leader from non-leader
			if (_state != msg.State) {
				_replicaLogPositions.Clear();
			}
			_state = msg.State;
		}

		public void Handle(SystemMessage.VNodeConnectionLost msg) {
			if (_state != VNodeState.Leader || !msg.SubscriptionId.HasValue) return;
			_replicaLogPositions.TryRemove(msg.SubscriptionId.Value, out _);
		}

		public void Handle(SystemMessage.BecomeShuttingDown message) {
			Stop();
		}

		public void Handle(SystemMessage.SystemInit message) {
			Start();
		}


	}
}
