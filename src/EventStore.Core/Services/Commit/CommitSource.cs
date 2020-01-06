using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.Commit {
	public class CommitSource : ICommitSource {
		private long _committedPosition;
		private long _logCommittedPosition;

		public long CommitPosition => Interlocked.Read(ref _committedPosition);

		public long LogCommittedPosition => Interlocked.Read(ref _logCommittedPosition);

		public void Handle(CommitMessage.CommittedTo message) {
			Interlocked.Exchange(ref _committedPosition, message.LogPosition);
			Notify(_notifyCommit, message.LogPosition);
		}

		public void Handle(CommitMessage.ReplicatedTo message) {
			Interlocked.Exchange(ref _logCommittedPosition, message.LogPosition);
			Notify(_notifyLogCommit, message.LogPosition);
		}

		private void Notify(ConcurrentDictionary<long, List<Action>> dictionary, long logPosition) {
			if (dictionary.IsEmpty) { return; }
			long[] positions;
			lock (dictionary) {
				positions = dictionary.Keys.ToArray();
			}
			Array.Sort(positions);
			var actions = new List<Action>();
			for (int i = 0; i < positions.Length && positions[i] <= logPosition; i++) {
				if (dictionary.TryRemove(positions[i], out actions) && actions != null) {
					lock (actions) {
						actions.AddRange(actions);
					}
				}
			}
			actions.ForEach(a => { try { a?.Invoke(); } catch { } });
		}

		private ConcurrentDictionary<long, List<Action>> _notifyCommit = new ConcurrentDictionary<long, List<Action>>();
		private ConcurrentDictionary<long, List<Action>> _notifyLogCommit = new ConcurrentDictionary<long, List<Action>>();
		public void NotifyCommitFor(long postition, Action target) {
			if (Interlocked.Read(ref _committedPosition) >= postition) { target(); }
			if (!_notifyCommit.TryGetValue(postition, out var actionList)) {
				actionList = new List<Action> { target };
				_notifyCommit.TryAdd(postition, actionList);
			} else {
				lock (actionList) {
					actionList.Add(target);
				}
			}
		}

		public void NotifyLogCommitFor(long postition, Action target) {
			if (Interlocked.Read(ref _logCommittedPosition) >= postition) { target(); }
			if (!_notifyLogCommit.TryGetValue(postition, out var actionList)) {
				actionList = new List<Action> { target };
				_notifyLogCommit.TryAdd(postition, actionList);
			} else {
				lock (actionList) {
					actionList.Add(target);
				}
			}
		}
	}
}
