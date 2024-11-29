// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO;
using DotNext.Text;

namespace EventStore.Core.Util;

internal static class AsyncBinaryReader {
	public static async ValueTask<string> ReadStringAsync(this IAsyncBinaryReader reader, DecodingContext context, CancellationToken token) {
		using var owner = await reader.DecodeAsync(context, LengthFormat.Compressed, token: token);
		return owner.ToString();
	}

	public static async ValueTask<byte[]> ReadBytesAsync(this IAsyncBinaryReader reader, int length, CancellationToken token) {
		var array = GC.AllocateUninitializedArray<byte>(length);
		await reader.ReadAsync(array, token);
		return array;
	}
}
