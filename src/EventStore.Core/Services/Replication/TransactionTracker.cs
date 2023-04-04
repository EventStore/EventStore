using System.Collections.Generic;
using System.Linq;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Replication;

public record TrackerRecord;
public record TrackerLogRecord : TrackerRecord {
	public ILogRecord LogRecord { get; init; }
}

public record CompleteChunkRecord : TrackerRecord {
	public int ChunkStartNumber { get; init; } = -1;
	public int ChunkEndNumber { get; init; } = -1;
}

public class TransactionTracker {
	private readonly List<TrackerRecord> _records = new();
	public IEnumerable<TrackerRecord> Records => _records;
	public bool IsChunkCompletionPending { get; private set; }

	public void Track(ILogRecord record, out bool canCommit) {
		_records.Add(new TrackerLogRecord {
			LogRecord = record
		});

		if (record is IPrepareLogRecord prepare
		    && prepare.Flags.HasFlag(PrepareFlags.IsCommitted)
		    && !prepare.Flags.HasFlag(PrepareFlags.TransactionEnd)) {
			canCommit = false;
			return;
		}

		// at the moment, all other log record types are atomic and can immediately be written to the log
		canCommit = true;
	}

	public bool CanCompleteChunk(int chunkStartNumber, int chunkEndNumber) {
		if (_records.Count == 0)
			return true;

		_records.Add(new CompleteChunkRecord {
			ChunkStartNumber = chunkStartNumber,
			ChunkEndNumber = chunkEndNumber
		});

		IsChunkCompletionPending = true;

		return false;
	}

	public void Clear() {
		_records.Clear();
		IsChunkCompletionPending = false;
	}
}
