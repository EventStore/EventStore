// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.LogV3 {
	public struct SlicedRecord {
		// todo: measure tradeoff between splitting these out on construction as we are
		// (=> larger struct to copy around) vs getting rid of this struct and just
		// splitting in the getters of the record
		// (=> smaller struct but probably repeated bounds checks)
		public Memory<byte> Bytes { get; init; }
		public Memory<byte> HeaderMemory { get; init; }
		public Memory<byte> SubHeaderMemory { get; init; }
		public Memory<byte> PayloadMemory { get; init; }
	}
}
