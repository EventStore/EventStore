// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV2;

public class LogV2StreamIdConverter : IStreamIdConverter<string> {
	public string ToStreamId(ReadOnlySpan<byte> bytes) {
		unsafe {
			fixed (byte* b = bytes) {
				return Encoding.UTF8.GetString(b, bytes.Length);
			}
		}
	}

	public string ToStreamId(ReadOnlyMemory<byte> bytes) =>
		ToStreamId(bytes.Span);
}
