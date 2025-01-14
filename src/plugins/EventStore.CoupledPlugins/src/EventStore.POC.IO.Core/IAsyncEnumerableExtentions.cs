// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EventStore.POC.IO.Core;

public static class IAsyncEnumerableExtentions {
	public static async Task ToChannelAsync<T>(
		this IAsyncEnumerable<T> self,
		ChannelWriter<T> writer,
		CancellationToken ct) {

		await foreach (var x in self.WithCancellation(ct)) {
			await writer.WriteAsync(x, ct);
		}
	}

	public static async IAsyncEnumerable<T> HandleStreamNotFound<T>(
		this IAsyncEnumerable<T> self) {

		await Task.Yield();

		await using var enumerator = self.GetAsyncEnumerator();
		while (true) {
			try {
				if (!await enumerator.MoveNextAsync())
					break;
			} catch (ResponseException.StreamNotFound) {
				break;
			}

			yield return enumerator.Current;
		}
	}
}
