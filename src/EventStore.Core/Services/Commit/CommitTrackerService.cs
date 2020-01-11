using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services.Commit {


	public class CommitTrackerService :
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<SystemMessage.SystemInit>,
		IHandle<CommitMessage.ReplicaWrittenTo>,
		IHandle<CommitMessage.WrittenTo>,
		IHandle<CommitMessage.IndexedTo>,
		IHandle<CommitMessage.MasterReplicatedTo> {
		private readonly ILogger _log = LogManager.GetLoggerFor<CommitTrackerService>();
		private readonly IPublisher _publisher;
		private readonly CommitLevel _level;
		private readonly ICheckpoint _replicationCheckpoint;
		private readonly int _quorumSize;
		private Thread _thread;
		private bool _stop;
		private VNodeState _state;
		private long _committedPosition;
		private long _previousCommittedPosition;
		private long _replicationPosition;
		private long _previousReplicationPosition;
		private long _writePosition;
		private long _indexedPosition;
		private long _idle = 1;
		private readonly ConcurrentDictionary<Guid, long> _replicaLogPositions = new ConcurrentDictionary<Guid, long>();

		private readonly ManualResetEventSlim _commitChanged = new ManualResetEventSlim(false, 1);
		private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();

		public Task Task {
			get { return _tcs.Task; }
		}

		public CommitTrackerService(IPublisher publisher, CommitLevel level, int clusterNodeCount, ICheckpoint replicationCheckpoint, ICheckpoint writerCheckpoint) {
			Ensure.NotNull(publisher, nameof(publisher));
			Ensure.NotNull(replicationCheckpoint, nameof(replicationCheckpoint));
			Ensure.NotNull(writerCheckpoint, nameof(writerCheckpoint));
			Ensure.Positive(clusterNodeCount, nameof(clusterNodeCount));
			_publisher = publisher;
			_level = level;
			_replicationCheckpoint = replicationCheckpoint;
			_quorumSize = clusterNodeCount / 2 + 1;
			_committedPosition = 0;
			_replicationPosition = replicationCheckpoint.Read();
			_writePosition = 0;
			_indexedPosition = 0;
		}

		public void Start() {
			_thread = new Thread(TrackReplication) { IsBackground = true, Name = nameof(CommitTrackerService) };
			_thread.Start();
		}

		public void Stop() {
			_stop = true;
		}
		public bool IsIdle() { return Interlocked.Read(ref _idle) == 1; }
		private void TrackReplication() {
			try {
				while (!_stop) {
					//todo: consider if this will be too spammy, should we have a throttle here?
					_commitChanged.Reset();
					if (_state == VNodeState.Master) {
						//Publish Log Commit Position
						var newPos = Interlocked.Read(ref _replicationPosition);
						var previousPos = Interlocked.Read(ref _previousReplicationPosition);
						while (newPos > previousPos) {
							if (Interlocked.CompareExchange(ref _previousReplicationPosition, newPos, previousPos) != previousPos) {
								newPos = Interlocked.Read(ref _replicationPosition);
								previousPos = Interlocked.Read(ref _previousReplicationPosition);								
							} else {
								_replicationCheckpoint.Write(newPos);
								_replicationCheckpoint.Flush();
								_publisher.Publish(new CommitMessage.ReplicatedTo(newPos));
							}
						}						
						//Publish Commit Position
						var commitPos = Interlocked.Read(ref _committedPosition);
						if (commitPos > Interlocked.Read(ref _previousCommittedPosition)) {
							_publisher.Publish(new CommitMessage.CommittedTo(commitPos));
							Interlocked.Exchange(ref _previousCommittedPosition, commitPos);
						}
					}
					_idle = 1;
					_commitChanged.Wait(100);
					_idle = 0;
				}
			} catch (Exception exc) {
				_log.FatalException(exc, $"Error in {nameof(CommitTrackerService)}. Terminating...");
				_tcs.TrySetException(exc);
				Application.Exit(ExitCode.Error,
					$"Error in {nameof(CommitTrackerService)}. Terminating...\nError: " + exc.Message);
				//todo: is this right, are we waiting for someone to clean us up???
				while (!_stop) {
					Thread.Sleep(100);
				}
			}
			_publisher.Publish(new SystemMessage.ServiceShutdown(nameof(CommitTrackerService)));
		}
		public long ReplicatedPosition => _replicationCheckpoint.ReadNonFlushed();
		public void Handle(CommitMessage.MasterReplicatedTo message) {
			if (_state != VNodeState.Master) {
				//Publish Log Commit Position
				Interlocked.Exchange(ref _replicationPosition, message.LogPosition);
				var newPos = Interlocked.Read(ref _replicationPosition);
				var previousPos = Interlocked.Read(ref _previousReplicationPosition);
				while (newPos > previousPos) {
					if (Interlocked.CompareExchange(ref _previousReplicationPosition, newPos, previousPos) != previousPos) {
						newPos = Interlocked.Read(ref _replicationPosition);
						previousPos = Interlocked.Read(ref _previousReplicationPosition);						
					} else {
						_replicationCheckpoint.Write(newPos);
						_replicationCheckpoint.Flush();
					}
				}
				_publisher.Publish(new CommitMessage.ReplicatedTo(newPos));
			}
		}
		private void UpdateCommitPosition() {

			UpdateLogCommittedPos();
			var logCommittedTo = Interlocked.Read(ref _replicationPosition);
			switch (_level) {
				case CommitLevel.MasterWrite:
					Interlocked.Exchange(ref _committedPosition, _writePosition);
					break;
				case CommitLevel.ClusterWrite:
					Interlocked.Exchange(ref _committedPosition, logCommittedTo);
					break;
				case CommitLevel.MasterIndexed:
					var commitPos = Math.Min(logCommittedTo, Interlocked.Read(ref _indexedPosition));
					Interlocked.Exchange(ref _committedPosition, commitPos);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			_commitChanged.Set();
		}

		private void UpdateLogCommittedPos() {
			if (_level == CommitLevel.MasterWrite) {
				Interlocked.Exchange(ref _replicationPosition, _writePosition);
				return;
			}
			switch (_level) {
				case CommitLevel.MasterWrite:
					Interlocked.Exchange(ref _replicationPosition, _writePosition);
					break;
				case CommitLevel.ClusterWrite:
				case CommitLevel.MasterIndexed:
					var logCommitted = Interlocked.Read(ref _replicationPosition);
					var masterPos = Interlocked.Read(ref _writePosition);
					if (masterPos <= logCommitted) { return; }

					var minReplicas = _quorumSize - 1; //total - master = min replicas
					if (minReplicas == 0) {
						Interlocked.Exchange(ref _replicationPosition, masterPos);
						return;
					}

					long[] positions;
					lock (_replicaLogPositions) {
						positions = _replicaLogPositions.Values.ToArray();
					}

					if (positions.Length < minReplicas) { return; }

					Array.Sort(positions);
					var furthestReplicatedPosition = positions[minReplicas - 1];
					if (furthestReplicatedPosition <= logCommitted) { return; }

					var logCommittedTo = Math.Min(masterPos, furthestReplicatedPosition);
					Interlocked.Exchange(ref _replicationPosition, logCommittedTo);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}


		public void Handle(CommitMessage.ReplicaWrittenTo message) {
			if (_state != VNodeState.Master) { return; }
			if (_replicaLogPositions.TryGetValue(message.ReplicaId, out var position) &&
				message.LogPosition <= position) { return; }
			_replicaLogPositions.AddOrUpdate(message.ReplicaId, message.LogPosition, (k, v) => message.LogPosition);
			UpdateCommitPosition();
		}

		public void Handle(CommitMessage.WrittenTo message) {
			if (_state != VNodeState.Master) { return; }
			if (message.LogPosition <= Interlocked.Read(ref _writePosition)) { return; }
			Interlocked.Exchange(ref _writePosition, message.LogPosition);
			UpdateCommitPosition();
		}

		public void Handle(CommitMessage.IndexedTo message) {
			if (_state != VNodeState.Master) { return; }
			if (message.LogPosition <= Interlocked.Read(ref _indexedPosition)) { return; }
			Interlocked.Exchange(ref _indexedPosition, message.LogPosition);
			UpdateCommitPosition();

		}

		public void Handle(SystemMessage.StateChangeMessage msg) {
			//switching to master from non-Master
			if (_state != msg.State ) {				
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
