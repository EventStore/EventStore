// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.LogV3 {
	public struct ReadOnlySlicedRecord {
		public ReadOnlyMemory<byte> Bytes { get; init; }
		public ReadOnlyMemory<byte> HeaderMemory { get; init; }
		public ReadOnlyMemory<byte> SubHeaderMemory { get; init; }
		public ReadOnlyMemory<byte> PayloadMemory { get; init; }
	}
}
