// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Runtime.InteropServices;

namespace EventStore.LogV3 {
	public interface IRecordView {
		ReadOnlyMemory<byte> Bytes { get; }
		ref readonly Raw.RecordHeader Header { get; }
		ReadOnlyMemory<byte> Payload { get; }
	}

	// Immutable, generic, view of a record
	public struct RecordView<TSubHeader> : IRecordView where TSubHeader : unmanaged {
		private readonly ReadOnlySlicedRecord _sliced;

		public ReadOnlyMemory<byte> Bytes => _sliced.Bytes;
		public ref readonly Raw.RecordHeader Header => ref MemoryMarshal.AsRef<Raw.RecordHeader>(_sliced.HeaderMemory.Span);
		public ref readonly T RecordId<T>() where T : unmanaged =>
			ref MemoryMarshal.AsRef<T>(_sliced.HeaderMemory[Raw.RecordHeader.RecordIdOffset..].Span);
		public ref readonly TSubHeader SubHeader => ref MemoryMarshal.AsRef<TSubHeader>(_sliced.SubHeaderMemory.Span);
		public ReadOnlyMemory<byte> Payload => _sliced.PayloadMemory;
		public int PayloadOffset => _sliced.HeaderMemory.Length + _sliced.SubHeaderMemory.Length;

		public RecordView(ReadOnlyMemory<byte> bytes) {
			_sliced = SlicedRecordCreator<TSubHeader>.Create(bytes);
		}
	}
}
