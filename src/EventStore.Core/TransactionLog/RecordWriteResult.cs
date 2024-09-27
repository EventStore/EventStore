// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.TransactionLog {
	public struct RecordWriteResult {
		public bool Success;
		public readonly long OldPosition;
		public readonly long NewPosition;

		public RecordWriteResult(bool success, long oldPosition, long newPosition) {
			if (newPosition < oldPosition)
				throw new ArgumentException("New position is less than old position.");

			Success = success;
			OldPosition = oldPosition;
			NewPosition = newPosition;
		}

		public static RecordWriteResult Failed(long position) {
			return new RecordWriteResult(false, position, position);
		}

		public static RecordWriteResult Successful(long oldPosition, long newPosition) {
			return new RecordWriteResult(true, oldPosition, newPosition);
		}

		public override string ToString() {
			return string.Format("Success: {0}, OldPosition: {1}, NewPosition: {2}", Success, OldPosition, NewPosition);
		}
	}
}
