// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class LogV3StreamIdConverter : IStreamIdConverter<StreamId> {
		public uint ToStreamId(System.ReadOnlySpan<byte> bytes) {
			throw new System.NotImplementedException();
		}

		public uint ToStreamId(System.ReadOnlyMemory<byte> bytes) {
			throw new System.NotImplementedException();
		}
	}
}
