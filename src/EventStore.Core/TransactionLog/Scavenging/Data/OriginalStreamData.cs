// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.TransactionLog.Scavenging;

public class OriginalStreamData {
	// Populated by Accumulator and Calculator. Read by Calculator and Cleaner.
	public CalculationStatus Status { get; set; }

	// Populated by Accumulator. Read by Calculator.
	// (MaxAge also read by ChunkExecutor)
	public long? MaxCount { get; set; }
	public TimeSpan? MaxAge { get; set; }
	public long? TruncateBefore { get; set; }
	public bool IsTombstoned { get; set; }

	// Populated by Calculator. Read by Calculator and Executors.
	public DiscardPoint DiscardPoint { get; set; }
	public DiscardPoint MaybeDiscardPoint { get; set; }

	public override string ToString() =>
		$"Status: {Status} " +
		$"MaxCount: {MaxCount} " +
		$"MaxAge: {MaxAge} " +
		$"TruncateBefore: {TruncateBefore} " +
		$"IsTombstoned: {IsTombstoned} " +
		$"DiscardPoint: {DiscardPoint} " +
		$"MaybeDiscardPoint: {MaybeDiscardPoint} " +
		"";
}
