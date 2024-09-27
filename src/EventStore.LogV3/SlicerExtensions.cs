// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Runtime.InteropServices;

namespace EventStore.LogV3 {
	public static class SlicerExtensions {
		public static ref T SliceAs<T>(this ref MemorySlicer<byte> slicer) where T : unmanaged =>
			ref MemoryMarshal.AsRef<T>(slicer.Slice<T>().Span);

		public unsafe static Memory<byte> Slice<T>(this ref MemorySlicer<byte> slicer) where T : unmanaged =>
			slicer.Slice(sizeof(T));

		public static ref readonly T SliceAs<T>(this ref ReadOnlyMemorySlicer<byte> slicer) where T : unmanaged =>
			ref MemoryMarshal.AsRef<T>(slicer.Slice<T>().Span);

		public unsafe static ReadOnlyMemory<byte> Slice<T>(this ref ReadOnlyMemorySlicer<byte> slicer) where T : unmanaged =>
			slicer.Slice(sizeof(T));
	}
}
