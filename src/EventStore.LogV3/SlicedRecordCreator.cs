// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.LogV3;

// This class slices bytes up according to the headers in the desired record
// It exists to keep the logic between the readonly/normal versions in the same place.
public static class SlicedRecordCreator<TSubHeader> where TSubHeader : unmanaged {
	public static SlicedRecord Create(Memory<byte> bytes) {
		var slicer = bytes.Slicer();
		return new SlicedRecord {
			Bytes = slicer.Remaining,
			HeaderMemory = slicer.Slice(Raw.RecordHeader.Size),
			SubHeaderMemory = slicer.Slice<TSubHeader>(),
			PayloadMemory = slicer.Remaining,
		};
	}

	public static ReadOnlySlicedRecord Create(ReadOnlyMemory<byte> bytes) {
		var slicer = bytes.Slicer();
		return new ReadOnlySlicedRecord {
			Bytes = slicer.Remaining,
			HeaderMemory = slicer.Slice(Raw.RecordHeader.Size),
			SubHeaderMemory = slicer.Slice<TSubHeader>(),
			PayloadMemory = slicer.Remaining,
		};
	}
}
