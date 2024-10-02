// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Diagnostics.Metrics;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Metrics;

public class LogicalChunkReadDistributionMetric {
	private readonly Histogram<long> _histogram;
	private readonly IReadOnlyCheckpoint _writer;
	private readonly int _chunkSize;

	public LogicalChunkReadDistributionMetric(Meter meter, string name, IReadOnlyCheckpoint writer, int chunkSize) {
		_histogram = meter.CreateHistogram<long>(name);
		_writer = writer;
		_chunkSize = chunkSize;
	}

	public void Record(ILogRecord record) {
		// todo: consider sampling if this turns out to have a performance implication.
		// in the mean time event read metrics can be turned off in metricsconfig.json
		var recordLogicalChunk = record.LogPosition / _chunkSize;
		var currentLogicalChunk = _writer.ReadNonFlushed() / _chunkSize;
		_histogram.Record(currentLogicalChunk - recordLogicalChunk);
	}
}