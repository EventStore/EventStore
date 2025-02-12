// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.POC.IO.Core;
using Polly;
using Polly.Retry;

namespace EventStore.POC.PluginHost;

public class ResilientEventStoreReader : IAsyncEnumerator<ResolvedEvent> {
	private const int MaxRetry = 10;

	private IAsyncEnumerator<ResolvedEvent> _enum;
	private readonly AsyncRetryPolicy _retryPolicy;
	private ResolvedEvent? _lastSeen;
	private long _maxCount;

	public static IAsyncEnumerable<ResolvedEvent> Create(
		Func<ResolvedEvent?, long, IAsyncEnumerable<ResolvedEvent>> createReader,
		Action<ResolvedEvent?, int, int> onRetry,
		long maxCount) {

		return new Enumerable(createReader, onRetry, maxCount);
	}

	private class Enumerable : IAsyncEnumerable<ResolvedEvent> {
		private readonly Func<ResolvedEvent?, long, IAsyncEnumerable<ResolvedEvent>> _createReader;
		private readonly Action<ResolvedEvent?, int, int> _onRetry;
		private readonly long _maxCount;

		public Enumerable(
			Func<ResolvedEvent?, long, IAsyncEnumerable<ResolvedEvent>> createReader,
			Action<ResolvedEvent?, int, int> onRetry,
			long maxCount) {

			_createReader = createReader;
			_onRetry = onRetry;
			_maxCount = maxCount;
		}

		public IAsyncEnumerator<ResolvedEvent> GetAsyncEnumerator(CancellationToken ct = new CancellationToken()) {
			return new ResilientEventStoreReader(_createReader, _onRetry, _maxCount, ct);
		}
	}

	private ResilientEventStoreReader(
		Func<ResolvedEvent?, long, IAsyncEnumerable<ResolvedEvent>> createReader,
		Action<ResolvedEvent?, int, int> onRetry,
		long maxCount,
		CancellationToken ct) {

		_enum = createReader(null, maxCount).GetAsyncEnumerator(ct);
		_retryPolicy = Policy
			.Handle<ResponseException.ServerBusy>()
			.Or<ResponseException.ServerNotReady>()
			.Or<ResponseException.Timeout>()
			.WaitAndRetryAsync(MaxRetry, RetryDelay, onRetry: (ex, _, retry, _) => {
				onRetry(_lastSeen, retry, MaxRetry);
				_enum = createReader(_lastSeen, _maxCount).GetAsyncEnumerator();
			});
	}

	public ResolvedEvent Current => _enum.Current;

	private static TimeSpan RetryDelay(int retryCount) =>
		TimeSpan.FromSeconds(retryCount * retryCount * 0.2);

	public async ValueTask DisposeAsync() {
		await _enum.DisposeAsync();
	}

	public async ValueTask<bool> MoveNextAsync() {
		//qq reduce allocations
		return await _retryPolicy.ExecuteAsync(async () => {
			if (await _enum.MoveNextAsync()) {
				_lastSeen = Current;
				_maxCount = _maxCount == long.MaxValue ? _maxCount : _maxCount - 1;
				return true;
			}

			return false;
		});
	}
}
