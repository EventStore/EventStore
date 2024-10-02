// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	public struct BulkReadResult {
		public readonly int OldPosition;
		public readonly int BytesRead;
		public readonly bool IsEOF;

		public BulkReadResult(int oldPosition, int bytesRead, bool isEof) {
			OldPosition = oldPosition;
			BytesRead = bytesRead;
			IsEOF = isEof;
		}
	}
}
