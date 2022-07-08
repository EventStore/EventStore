using System;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class OriginalStreamData {

		public OriginalStreamData() {
		}

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
}
