using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using EventStore.Core.Services.Storage;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.Services.Storage {
	public class FakeIndexCommitterService<TStreamId> : IIndexCommitterService<TStreamId> {
		public Dictionary<Guid, Transaction> Transactions = new Dictionary<Guid, Transaction>();
		public List<ILogRecord> Records = new List<ILogRecord>();
		public long PostPosition;
		public void AddPendingCommit(CommitLogRecord commit, long postPosition) {
			if (commit == null)
				throw new InvalidOperationException("Cannot commit a null transaction");
			if (Transactions.TryGetValue(commit.CorrelationId, out var transaction)) {
				transaction.CommitTransaction(commit);
				Records.Add(commit);
			} else {
				throw new InvalidOperationException("Cannot commit an unknown transaction");
			}
			PostPosition = postPosition;
		}

		public bool TryGetFirstEventNumberForPendingTransaction(long transactionPosition, out long firstEventNumber) {
			foreach (var kvp in Transactions) {
				if (kvp.Value.Prepares.Count > 0 && kvp.Value.Prepares[0].LogPosition == transactionPosition) {
					firstEventNumber = kvp.Value.FirstEventNumber;
					return true;
				}
			}

			firstEventNumber = -1;
			return false;
		}

		public void AddPendingPrepare(IPrepareLogRecord<TStreamId>[] prepares, long postPosition) {
			if (prepares == null)
				throw new InvalidOperationException("Cannot commit a null transaction");
			if (prepares.Length <= 0)
				return;
			if (!Transactions.TryGetValue(prepares[0].CorrelationId, out var transaction)) {
				transaction = new Transaction(prepares[0].CorrelationId, prepares[0].ExpectedVersion + 1);
				Transactions.Add(prepares[0].CorrelationId, transaction);
				Records.AddRange(prepares);
			}
			transaction.AddPrepares(prepares);
			PostPosition = postPosition;
		}

		public long GetCommitLastEventNumber(CommitLogRecord record) {
			if (Transactions.TryGetValue(record.CorrelationId, out var transaction)) {
				return record.FirstEventNumber + transaction.Prepares.Count;
			} else {
				throw new InvalidOperationException("Cannot get last event number for an unknown transaction");
			}
		}

		public void Init(long checkpointPosition) { }

		public void Stop() { }

		public class Transaction {
			public Guid Id { get; }
			public bool IsCommitted => Commit != null;
			public CommitLogRecord Commit { get; private set; }
			public ReadOnlyCollection<IPrepareLogRecord<TStreamId>> Prepares => _prepares.AsReadOnly();
			private List<IPrepareLogRecord<TStreamId>> _prepares = new List<IPrepareLogRecord<TStreamId>>();
			public long FirstEventNumber { get; }

			public Transaction(Guid id, long firstEventNumber, ICollection<IPrepareLogRecord<TStreamId>> prepares = null) {
				Id = id;
				FirstEventNumber = firstEventNumber;
				if (prepares != null) { AddPrepares(prepares); }
			}
			public void CommitTransaction(CommitLogRecord commit) {
				Commit = commit;
			}
			public void AddPrepares(ICollection<IPrepareLogRecord<TStreamId>> prepares) {
				if (IsCommitted) { throw new InvalidOperationException("Cannot add data to a committed transation"); }
				_prepares.AddRange(prepares);
			}
		}
	}
}
