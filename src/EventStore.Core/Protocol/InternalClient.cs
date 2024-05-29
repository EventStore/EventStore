using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Common;
using EventStore.Core.Services.Transport.Enumerators;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.LogRecords;
using Serilog;
namespace EventStore.Core.Protocol;

public class InternalClient : IClient {
	private static readonly ILogger Log = Serilog.Log.ForContext<InternalClient>();

	private const int MaxLiveEventBufferCount = 32;

	private readonly IPublisher _publisher;

	private static readonly BoundedChannelOptions _boundedChannelOptions =
		new(MaxLiveEventBufferCount) {
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true,
			SingleWriter = true
		};

	public InternalClient(IPublisher publisher) {
		_publisher = publisher;
	}

	public async Task<long> WriteAsync(string stream, EventToWrite[] events, long expectedVersion,
		CancellationToken cancellationToken) {

		var appendResponseSource = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
		var envelope = new CallbackEnvelope(HandleWriteEventsCompleted);

		//qq consider all the args
		var correlationId = Guid.NewGuid(); //qq
		_publisher.Publish(new ClientMessage.WriteEvents(
			correlationId,
			correlationId,
			envelope,
			requireLeader: false,
			stream,
			expectedVersion,
			events.Select(evt =>
				new Core.Data.Event(
					evt.EventId, evt.EventType, isJson: evt.ContentType == "application/json",
					evt.Data.ToArray(),
					evt.Metadata.ToArray())).ToArray(),
			SystemAccounts.System,
			cancellationToken: cancellationToken));

		return await appendResponseSource.Task.ConfigureAwait(false);

		void HandleWriteEventsCompleted(EventStore.Core.Messaging.Message message) {
			if (message is ClientMessage.NotHandled notHandled) {
				Exception ex = notHandled.Reason switch {
					ClientMessage.NotHandled.Types.NotHandledReason.NotReady => new ResponseException.ServerNotReady(),
					ClientMessage.NotHandled.Types.NotHandledReason.TooBusy => new ResponseException.ServerBusy(),
					ClientMessage.NotHandled.Types.NotHandledReason.NotLeader or
						ClientMessage.NotHandled.Types.NotHandledReason.IsReadOnly => new ResponseException.NotLeader(),
					_ => new ResponseException.UnexpectedError($"NotHandled {notHandled.Reason}"),
				};
				appendResponseSource.TrySetException(ex);
				return;
			}

			if (message is not ClientMessage.WriteEventsCompleted completed) {
				appendResponseSource.TrySetException(
					new ResponseException.UnexpectedError($"Unexpected message {message}"));
				return;
			}

			//qqqqqqqq
			switch (completed.Result) {
				case OperationResult.Success:
					appendResponseSource.TrySetResult(completed.LastEventNumber);
					return;
				case OperationResult.PrepareTimeout:
				case OperationResult.CommitTimeout:
				case OperationResult.ForwardTimeout:
					appendResponseSource.TrySetException(new ResponseException.Timeout(completed.Message));
					return;
				case OperationResult.WrongExpectedVersion:
					//qq irl this is much more complicated see Streams.Append.cs
					appendResponseSource.TrySetException(new ResponseException.WrongExpectedVersion(completed.Message));
					return;
				case OperationResult.StreamDeleted:
					appendResponseSource.TrySetException(new ResponseException.StreamDeleted(completed.Message));
					return;
				case OperationResult.InvalidTransaction:
					appendResponseSource.TrySetException(new ResponseException.UnexpectedError(completed.Message));
					return;
				case OperationResult.AccessDenied:
					appendResponseSource.TrySetException(new ResponseException.AccessDenied(completed.Message));
					return;
				default:
					appendResponseSource.TrySetException(new ResponseException.UnexpectedError(completed.Message));
					return;
			}
		}
	}

	public IAsyncEnumerable<Event> ReadStreamForwards(string stream, StreamRevision start, long maxCount, CancellationToken token) =>
    		Create(
    			$"Reading stream {stream} forwards for max {maxCount} events",
    			() => new Enumerator.ReadStreamForwards(
    				bus: _publisher,
    				streamName: stream,
    				startRevision: start,
    				maxCount: (ulong)maxCount,
    				resolveLinks: false,
    				user: SystemAccounts.System,
    				requiresLeader: false,
    				deadline: DateTime.UtcNow.AddSeconds(10),
    				cancellationToken: token,
    				compatibility: 1));

	public IAsyncEnumerable<Event> ReadStreamBackwards(string stream, long maxCount, CancellationToken token) =>
		Create(
			$"Reading stream {stream} backwards for max {maxCount} events",
			() => new Enumerator.ReadStreamBackwards(
				bus: _publisher,
				streamName: stream,
				startRevision: StreamRevision.End,
				maxCount: (ulong)maxCount,
				resolveLinks: false,
				user: SystemAccounts.System,
				requiresLeader: false,
				deadline: DateTime.UtcNow.AddSeconds(10),
				cancellationToken: token,
				compatibility: 1));

	private static IAsyncEnumerable<Event> Create(
		string message,
		Func<IAsyncEnumerator<ReadResponse>> enumeratorFactory) {

		return new LoggingEnumerable<ReadResponse>(enumeratorFactory, message)
			.MapExceptions()
			.Select(x => {
				if (x is ReadResponse.StreamNotFound y)
					throw new ResponseException.StreamNotFound(y.StreamName);
				return x;
			})
			.OfType<ReadResponse.EventReceived>()
			.Select(x => ConvertEvent(ref x.Event));
	}

	private static Event ConvertEvent(ref ResolvedEvent evt) {
		var e = evt.OriginalEvent;
		return new Event(
			eventId: e.EventId,
			created: e.TimeStamp,
			stream: e.EventStreamId,
			eventNumber: (ulong)e.EventNumber,
			eventType: e.EventType,
			contentType: e.IsJson ? "application/json" : "application/octet-stream",
			commitPosition: (ulong)evt.OriginalPosition!.Value.CommitPosition,
			preparePosition: (ulong)evt.OriginalPosition!.Value.PreparePosition,
			isRedacted: e.Flags.HasAllOf(PrepareFlags.IsRedacted),
			data: e.Data,
			metadata: e.Metadata);
	}

	class LoggingEnumerable<T> : IAsyncEnumerable<T> {
		private readonly Func<IAsyncEnumerator<T>> _enumeratorFactory;
		private readonly string _message;

		public LoggingEnumerable(Func<IAsyncEnumerator<T>> enumeratorFactory, string message) {
			_enumeratorFactory = enumeratorFactory;
			_message = message;
		}

		public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) {
			return new LoggingEnumerator<T>(_enumeratorFactory(), _message);
		}
	}

	sealed class LoggingEnumerator<T> : IAsyncEnumerator<T> {
		private readonly string _message;
		private readonly IAsyncEnumerator<T> _wrapped;

		public LoggingEnumerator(IAsyncEnumerator<T> wrapped, string message) {
			_wrapped = wrapped;
			_message = message;
			Log.Information("Internal Client: Started {message}", _message);
		}

		public T Current => _wrapped.Current;

		public ValueTask DisposeAsync() {
			Log.Information("Internal Client: Ended {message}", _message);
			return _wrapped.DisposeAsync();
		}

		public ValueTask<bool> MoveNextAsync() {
			return _wrapped.MoveNextAsync();
		}
	}
}

public static partial class IAsyncEnumerableExtenions {
	public static bool TryMap(this Exception ex, out Exception mapped) {
		mapped = ex.Map();
		return mapped != ex;
	}

	private static Exception Map(this Exception ex) => ex switch {
		ReadResponseException.AccessDenied =>
			new ResponseException.AccessDenied(ex),

		ReadResponseException.NotHandled.ServerNotReady =>
			new ResponseException.ServerNotReady(ex),

		ReadResponseException.NotHandled.ServerBusy =>
			new ResponseException.ServerBusy(ex),

		ReadResponseException.NotHandled.LeaderInfo or
			ReadResponseException.NotHandled.NoLeaderInfo =>
			new ResponseException.NotLeader(ex),

		ReadResponseException.StreamDeleted =>
			new ResponseException.StreamDeleted(ex),

		ReadResponseException.Timeout =>
			new ResponseException.Timeout(ex),

		OperationCanceledException =>
			ex,

		ReadResponseException.InvalidPosition or
			ReadResponseException.UnknownError or
			ReadResponseException.UnknownMessage or
			_ =>
			new ResponseException.UnexpectedError(ex),
	};

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