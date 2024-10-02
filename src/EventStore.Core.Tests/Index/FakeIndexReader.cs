// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.Fakes {
	public class FakeIndexReader : ITransactionFileReader {
		private readonly Func<long, bool> _existsAt;

		public FakeIndexReader(Func<long, bool> existsAt = null) {
			_existsAt = existsAt ?? (l => true);
		}

		public void Reposition(long position) {
			throw new NotImplementedException();
		}

		public SeqReadResult TryReadNext() {
			throw new NotImplementedException();
		}

		public SeqReadResult TryReadPrev() {
			throw new NotImplementedException();
		}

		public RecordReadResult TryReadAt(long position, bool couldBeScavenged) {
			var record = (LogRecord)new PrepareLogRecord(position, Guid.NewGuid(), Guid.NewGuid(), 0, 0,
				position.ToString(), null, -1, DateTime.UtcNow, PrepareFlags.None, "type", null,
				new byte[0], null);
			return new RecordReadResult(true, position + 1, record, 1);
		}

		public bool ExistsAt(long position) {
			return _existsAt(position);
		}
	}
}
