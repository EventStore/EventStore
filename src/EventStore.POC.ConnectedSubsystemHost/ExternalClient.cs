// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.POC.IO.Core;
using Grpc.Core;
using Polly;
using Serilog;

namespace EventStore.POC.PluginHost;

internal class ExternalClient : IClient {
	private static readonly ILogger _logger = Log.ForContext<ExternalClient>();
	
	private const int MaxRetry = 10;
	private const int MaxLiveEventBufferCount = 32;

	private readonly EventStoreClient _client;

	private static readonly BoundedChannelOptions _boundedChannelOptions =
		new(MaxLiveEventBufferCount) {
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true,
			SingleWriter = true
		};

	public ExternalClient(EventStoreClient client) {
		_client = client;
	}

	private Task SubscribeAsync(
		Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
		CancellationToken cancellationToken,
		Client.Position checkpoint) {

		Client.FromAll start;

		if (checkpoint == Client.Position.Start)
			start = Client.FromAll.Start;
		else if (checkpoint == Client.Position.End)
			start = Client.FromAll.End;
		else 
			start = Client.FromAll.After(checkpoint);

		_logger.Information("Subscribing to $all from {start}", start);

		// run in the background
		_ = WithRetry(
			action: async () => {
				var tcs = new TaskCompletionSource();
				var result = await _client
					.SubscribeToAllAsync(
						start: start,
						eventAppeared: async (s, evt, ct) => {
							await eventAppeared(s, evt, ct);
							start = Client.FromAll.After(evt.OriginalPosition!.Value);
						},
						resolveLinkTos: false,
						subscriptionDropped: SubscriptionDropped("$all", tcs),
						filterOptions: null,
						userCredentials: null,
						cancellationToken: cancellationToken)
					.MapExceptions();

				_logger.Information(
					"Subscription {subscription} to $all successfully started after {start}.",
					result.SubscriptionId, start);

				await tcs.Task;

				return result;
			},
			onRetry: LogSubscriptionInterruption("$all"));
		
		return Task.CompletedTask;
	}

	private Task SubscribeToStreamAsync(string stream,
		Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
		StreamPosition? checkpoint = null) {

		var start = checkpoint == null ? FromStream.Start : FromStream.After(checkpoint.Value);
		_logger.Information("Subscribing to {stream} from {start}", stream, start);
		
		// run in the background
		_ = WithRetry(
			action: async () => {
				var tcs = new TaskCompletionSource();
				var result = await _client
					.SubscribeToStreamAsync(
						streamName: stream,
						start: start,
						eventAppeared: async (s, evt, ct) => {
							await eventAppeared(s, evt, ct);
							start = FromStream.After(evt.OriginalEventNumber);
						},
						resolveLinkTos: false,
						subscriptionDropped: SubscriptionDropped(stream, tcs),
						userCredentials: null,
						cancellationToken: default)
					.MapExceptions();

				_logger.Information(
					"Subscription {subscription} to {stream} successfully started after {start}.",
					result.SubscriptionId, stream, start);

				await tcs.Task;
				
				return result;
			},
			onRetry: LogSubscriptionInterruption(stream));
		
		return Task.CompletedTask;
	}

	public async Task<long> WriteAsync(string stream, EventToWrite[] events, long expectedVersion, CancellationToken ct) {
		var es = events.Select(evt => new EventData(Uuid.FromGuid(evt.EventId), evt.EventType, evt.Data, evt.Metadata, evt.ContentType)).ToArray();
		IWriteResult res = await WithRetry(
			action: async () => {
				if (expectedVersion == ExpectedVersion.Any) {
					//qq other args
					return await _client
						.AppendToStreamAsync(stream, StreamState.Any, es, cancellationToken: ct)
						.MapExceptions();
				} else {
					return await _client
						.AppendToStreamAsync(stream, StreamRevision.FromInt64(expectedVersion), es, cancellationToken: ct)
						.MapExceptions();
				}
			},
			onRetry: (ex, retry, maxRetry) =>
				_logger.Warning(
					ex,
					"Failed to write to stream {stream}, retrying ({retry}/{maxRetry})",
					stream, retry, maxRetry));

		//qq res.
		return res.NextExpectedStreamRevision.ToInt64();
	}

	public async Task DeleteStreamAsync(string stream, long expectedVersion, CancellationToken cancellationToken) {
		await WithRetry(
			action: async () => {
				var deleteTask = expectedVersion == ExpectedVersion.Any
					? _client.DeleteAsync(
						streamName: stream,
						expectedState: StreamState.Any,
						cancellationToken: cancellationToken)
					: _client.DeleteAsync(
						streamName: stream,
						expectedRevision: StreamRevision.FromInt64(expectedVersion),
						cancellationToken: cancellationToken);

				return await deleteTask.MapExceptions();
			},
			onRetry: (ex, retry, maxRetry) =>
				_logger.Warning(
					ex,
					"Failed to delete stream {stream}, retrying ({retry}/{maxRetry})",
					stream, retry, maxRetry));
	}

	public async Task WriteMetaDataMaxCountAsync(string stream, CancellationToken cancellationToken) {
		await WithRetry(
			action: async () => {
				return await _client
					.SetStreamMetadataAsync(
						streamName: stream,
						expectedState: StreamState.Any,
						metadata: new StreamMetadata(maxCount: 3),
						configureOperationOptions: null,
						deadline: null,
						userCredentials: null,
						cancellationToken: cancellationToken)
					.MapExceptions();
			},
			onRetry: (ex, retry, maxRetry) =>
				_logger.Warning(
					ex,
					"Failed to write metadata to stream {stream}, retrying ({retry}/{maxRetry})",
					stream, retry, maxRetry));
	}

	private static Client.Position Convert(IO.Core.FromAll start) {
		if (start == IO.Core.FromAll.Start)
			return Client.Position.Start;
		else if (start == IO.Core.FromAll.End)
			return Client.Position.End;
		else
			return Convert(start.Position!.Value);
	}

	private static Client.Position Convert(IO.Core.Position position) =>
		new(position.CommitPosition, position.PreparePosition);

	public async IAsyncEnumerable<Event> SubscribeToAll(IO.Core.FromAll start, [EnumeratorCancellation] CancellationToken cancellationToken) {
		_logger.Information("Started subscription to $all from {start}", start);

		var channel = Channel.CreateBounded<Event>(_boundedChannelOptions);

		await SubscribeAsync(async (sub, evt, ct) => {
			await channel.Writer.WriteAsync(Convert(evt), cancellationToken);
		}, cancellationToken, Convert(start));

		//qq not certain we want to pass the token here
		//qq seems pretty ugly
		await foreach (var e in channel.Reader.ReadAllAsync(cancellationToken))
			yield return e;
	}

	public async IAsyncEnumerable<Event> SubscribeToStream(string stream, [EnumeratorCancellation] CancellationToken cancellationToken) {
		_logger.Information("Started subscription to {stream} from the start", stream);

		var channel = Channel.CreateBounded<Event>(_boundedChannelOptions);

		await SubscribeToStreamAsync(stream, async (sub, evt, ct) => {
			await channel.Writer.WriteAsync(Convert(evt), cancellationToken);
		});

		//qq not certain we want to pass the token here
		//qq seems pretty ugly
		await foreach (var e in channel.Reader.ReadAllAsync(cancellationToken))
			yield return e;
	}

	public IAsyncEnumerable<Event> ReadStreamBackwards(string stream, long maxCount, CancellationToken cancellationToken) {
		return ResilientEventStoreReader.Create(
			createReader: (lastSeen, mc) => _client
				.ReadStreamAsync(Direction.Backwards, stream, StartFrom(lastSeen), mc, cancellationToken: cancellationToken)
				.MapExceptions(),
			onRetry: (lastSeen, retry, maxRetry) =>
				LogReadInterruption(stream, StartFrom(lastSeen).ToString(), retry, maxRetry),
			maxCount)
		.Select(Convert);

		StreamPosition StartFrom(ResolvedEvent? lastSeen) {
			return lastSeen?.OriginalEventNumber ?? StreamPosition.End;
		}
	}

	public IAsyncEnumerable<Event> ReadAllBackwardsAsync(IO.Core.Position position, long maxCount, CancellationToken cancellationToken) {
		return ResilientEventStoreReader.Create(
			createReader: (lastSeen, mc) => _client
				.ReadAllAsync(Direction.Backwards, StartFrom(lastSeen), mc, cancellationToken: cancellationToken)
				.MapExceptions(),
			onRetry: (lastSeen, retry, maxRetry) =>
				LogReadInterruption("$all", StartFrom(lastSeen).ToString(), retry, maxRetry),
			maxCount)
		.Select(Convert);

		Client.Position StartFrom(ResolvedEvent? lastSeen) {
			return lastSeen != null && lastSeen.Value.OriginalPosition != null
				? lastSeen.Value.OriginalPosition!.Value
				: Client.Position.End;
		}
	}

	public IAsyncEnumerable<Event> ReadStreamForwards(string stream, long maxCount, CancellationToken cancellationToken) {
		return ResilientEventStoreReader.Create(
			createReader: (lastSeen, mc) => _client
				.ReadStreamAsync(Direction.Forwards, stream, StartFrom(lastSeen), mc, cancellationToken: cancellationToken)
				.MapExceptions(),
			onRetry: (lastSeen, retry, maxRetry) =>
				LogReadInterruption(stream, StartFrom(lastSeen).ToString(), retry, maxRetry),
			maxCount)
		.Select(Convert);

		StreamPosition StartFrom(ResolvedEvent? lastSeen) {
			return lastSeen?.OriginalEventNumber ?? StreamPosition.Start;
		}
	}

	private static Task<T> WithRetry<T>(Func<Task<T>> action, Action<Exception,int,int> onRetry) {
		var retryPolicy = Policy
			.Handle<ResponseException.ServerBusy>()
			.Or<ResponseException.ServerNotReady>()
			.Or<ResponseException.SubscriptionDropped>()
			.Or<ResponseException.Timeout>()
			.WaitAndRetryAsync(
				MaxRetry,
				RetryDelay,
				onRetry: (ex, _, retry, _) => onRetry(ex, retry, MaxRetry));

		return retryPolicy
			.ExecuteAsync(action);
	}
	
	private static TimeSpan RetryDelay(int retryCount) =>
		TimeSpan.FromSeconds(retryCount * retryCount * 0.2);
	
	private static Action<StreamSubscription, SubscriptionDroppedReason, Exception?>? SubscriptionDropped(string stream,
		TaskCompletionSource tcs) {
		return (sub, reason, ex) => {
			if (reason == SubscriptionDroppedReason.Disposed) {
				_logger.Warning(
					"Subscription {subscription} to {stream} was disposed {reason}",
					sub.SubscriptionId, stream, reason);
				tcs.TrySetResult();
				return;
			}

			_logger.Warning(
				ex,
				"Subscription {subscription} to {stream} was dropped {reason}. Resubscribing...",
				sub.SubscriptionId, stream, reason);

			tcs.TrySetException(new ResponseException.SubscriptionDropped(stream, reason.ToString()));
		};
	}
	
	private static Action<Exception, int, int> LogSubscriptionInterruption(string stream) {
		return (ex, retry, maxRetry) =>
			_logger.Warning(
				ex,
				"Subscription to {stream} got interrupted, retrying ({retry}/{maxRetry})",
				stream, retry, maxRetry);
	}
	
	private static void LogReadInterruption(string stream, string checkpoint, int retry, int maxRetry) {
		_logger.Warning(
			"Reading stream {stream} {direction} got interrupted, retrying from {checkpoint} ({retry}/{maxRetry})",
			stream, Direction.Backwards, checkpoint, retry, maxRetry);
	}
	
	private static Event Convert(ResolvedEvent evt) {
		return new Event(
			eventId: evt.OriginalEvent.EventId.ToGuid(), //qq
			created: evt.OriginalEvent.Created,
			stream: evt.OriginalEvent.EventStreamId,
			eventNumber: evt.OriginalEventNumber,
			eventType: evt.OriginalEvent.EventType,
			contentType: evt.OriginalEvent.ContentType,
			commitPosition: evt.OriginalPosition!.Value.CommitPosition,
			preparePosition: evt.OriginalPosition.Value.PreparePosition,
			isRedacted: false, //qq
			data: evt.OriginalEvent.Data,
			metadata: evt.OriginalEvent.Metadata);
	}
}

public static class Extensions {
	public static bool TryMap(this Exception ex, out Exception mapped) {
		mapped = ex.Map();
		return mapped != ex;
	}

	private static Exception Map(this Exception ex) => ex switch {
		AccessDeniedException =>
			new ResponseException.AccessDenied(ex),

		NotAuthenticatedException =>
			new ResponseException.NotAuthenticated(ex),

		NotLeaderException =>
			new ResponseException.NotLeader(ex),

		StreamDeletedException =>
			new ResponseException.StreamDeleted(ex),

		StreamNotFoundException snfe =>
			new ResponseException.StreamNotFound(snfe.Stream),
		
		ResponseException.SubscriptionDropped => ex,

		RpcException rex when rex.StatusCode == StatusCode.Unavailable =>
			new ResponseException.ServerNotReady(ex),

		RpcException rex when rex.StatusCode == StatusCode.DeadlineExceeded =>
			new ResponseException.Timeout(ex),

		RpcException rex when rex.StatusCode == StatusCode.Cancelled =>
			new OperationCanceledException("Call cancelled", ex),

		WrongExpectedVersionException =>
			new ResponseException.WrongExpectedVersion(ex),

		OperationCanceledException =>
			ex,

		ConnectionStringParseException or
		DiscoveryException or
		InvalidTransactionException or
		MaximumAppendSizeExceededException or
		RequiredMetadataPropertyMissingException or
		RpcException or
		ScavengeNotFoundException or
		UserNotFoundException or
		_ =>
			new ResponseException.UnexpectedError(ex),
	};


	public static async Task<T> MapExceptions<T>(this Task<T> self) {
		try {
			return await self;
		} catch (Exception ex) when (ex.TryMap(out var mapped)) {
			throw mapped;
		}
	}

	public static async IAsyncEnumerable<T> MapExceptions<T>(
		this IAsyncEnumerable<T> self) {

		await Task.Yield();

		await using var enumerator = self.GetAsyncEnumerator();
		while (true) {
			try {
				if (!await enumerator.MoveNextAsync())
					break;

			} catch (Exception ex) when (ex.TryMap(out var mapped)) {
				throw mapped;
			}

			yield return enumerator.Current;
		}
	}
}
