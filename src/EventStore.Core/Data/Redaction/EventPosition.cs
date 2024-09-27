// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data.Redaction {
	public readonly struct EventPosition {
		public long LogPosition { get; }
		public ChunkInfo ChunkInfo { get; }

		public EventPosition(long logPosition, string chunkFile, byte chunkVersion, bool chunkComplete, uint chunkEventOffset) {
			LogPosition = logPosition;
			ChunkInfo = new ChunkInfo(chunkFile, chunkVersion, chunkComplete, chunkEventOffset);
		}
	}

	public readonly struct ChunkInfo {
		public string FileName { get; }
		public byte Version { get; }
		public bool IsComplete { get; }
		public uint EventOffset { get; }

		public ChunkInfo(string fileName, byte version, bool isComplete, uint eventOffset) {
			FileName = fileName;
			Version = version;
			IsComplete = isComplete;
			EventOffset = eventOffset;
		}
	}
}
