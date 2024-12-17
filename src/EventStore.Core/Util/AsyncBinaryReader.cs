// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO;
using DotNext.Text;

namespace EventStore.Core.Util;

internal static class AsyncBinaryReader {
	public static string ReadString(this ref SequenceReader reader, in DecodingContext context) {
		using var buffer = reader.Decode(in context, LengthFormat.Compressed);
		return buffer.ToString();
	}

	public static byte[] ReadBytes(this ref SequenceReader reader, int length)
		=> reader.Read(length).ToArray();
}
