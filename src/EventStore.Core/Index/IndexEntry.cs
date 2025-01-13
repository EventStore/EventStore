// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Runtime.InteropServices;

namespace EventStore.Core.Index;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct IndexEntry(ulong stream, long version, long position) : IComparable<IndexEntry>, IEquatable<IndexEntry> {
	[FieldOffset(0)] public fixed byte Bytes [24];
	[FieldOffset(0)] public Int64 Version = version;
	[FieldOffset(8)] public UInt64 Stream = stream;
	[FieldOffset(16)] public Int64 Position = position;

	public int CompareTo(IndexEntry other) {
		var keyCmp = Stream.CompareTo(other.Stream);
		if (keyCmp != 0)
			return keyCmp;

		keyCmp = Version.CompareTo(other.Version);
		return keyCmp != 0 ? keyCmp : Position.CompareTo(other.Position);
	}

	public bool Equals(IndexEntry other) {
		return Stream == other.Stream && Version == other.Version && Position == other.Position;
	}

	public override string ToString() {
		return $"Stream: {Stream}, Version: {Version}, Position: {Position}";
	}
}
