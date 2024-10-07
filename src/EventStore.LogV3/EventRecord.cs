// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace EventStore.LogV3;

// View of an event in a stream write record
public struct EventRecord {
	private readonly ReadOnlyMemory<byte> _headerMemory;
	private readonly ReadOnlyMemory<byte> _data;
	private readonly ReadOnlyMemory<byte> _metadata;

	public ref readonly Raw.EventHeader Header => ref MemoryMarshal.AsRef<Raw.EventHeader>(_headerMemory.Span);
	public ReadOnlyMemory<byte> Data => _data;
	public ReadOnlyMemory<byte> Metadata => _metadata;
	public EventSystemMetadata SystemMetadata { get; }

	// bytes already populated with a event record to read
	public EventRecord(ReadOnlyMemory<byte> bytes) {
		var slicer = bytes.Slicer();
		_headerMemory = slicer.Slice(Raw.EventHeader.Size);

		ref readonly var header = ref MemoryMarshal.AsRef<Raw.EventHeader>(_headerMemory.Span);

		var systemMetadata = slicer.Slice(header.SystemMetadataSize);
		_data = slicer.Slice(header.DataSize);
		_metadata = slicer.Remaining;

		SystemMetadata = EventSystemMetadata.Parser.ParseFrom(new ReadOnlySequence<byte>(systemMetadata));
	}
}
