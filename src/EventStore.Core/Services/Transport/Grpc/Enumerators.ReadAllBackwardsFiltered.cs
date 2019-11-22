using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Grpc;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Enumerators {
		public class ReadAllBackwardsFiltered : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly int _maxCount;
			private readonly int _maxSearchWindow;
			private readonly Util.IEventFilter _eventFilter;
			private readonly bool _resolveLinks;
			private readonly IPrincipal _user;
			private readonly CancellationTokenSource _disposedTokenSource;
			private readonly ConcurrentQueue<ResolvedEvent> _buffer;

			private Position _nextPosition;
			private bool _isEnd;
			private ResolvedEvent _current;

			public ResolvedEvent Current => _current;

			public ReadAllBackwardsFiltered(
				IPublisher bus,
				Position position,
				int maxCount,
				bool resolveLinks,
				Util.IEventFilter eventFilter,
				int? maxSearchWindow,
				IPrincipal user,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (eventFilter == null) {
					throw new ArgumentNullException(nameof(eventFilter));
				}

				if (maxCount <= 0) {
					throw new ArgumentOutOfRangeException(nameof(maxCount));
				}

				if (maxSearchWindow.HasValue && maxSearchWindow.Value <= maxCount) {
					throw new ArgumentOutOfRangeException(nameof(maxSearchWindow));
				}

				_bus = bus;
				_nextPosition = position;
				_maxCount = maxCount;
				_eventFilter = eventFilter;
				_maxSearchWindow = maxSearchWindow ?? maxCount;
				_resolveLinks = resolveLinks;
				_user = user;
				_disposedTokenSource = new CancellationTokenSource();
				_buffer = new ConcurrentQueue<ResolvedEvent>();
				cancellationToken.Register(_disposedTokenSource.Dispose);
			}

			public ValueTask DisposeAsync() {
				_disposedTokenSource.Dispose();
				return default;
			}

			public async ValueTask<bool> MoveNextAsync() {
				if (_buffer.TryDequeue(out var current)) {
					_current = current;
					return true;
				}

				if (_isEnd) {
					return false;
				}

				var readNextSource = new TaskCompletionSource<bool>();

				var correlationId = Guid.NewGuid();

				var (commitPosition, preparePosition) = _nextPosition.ToInt64();

				_bus.Publish(new ClientMessage.ReadAllEventsBackwardFiltered(
					correlationId, correlationId, new CallbackEnvelope(OnMessage),
					commitPosition, preparePosition, Math.Min(_maxCount, 32),
					_resolveLinks, false, _maxSearchWindow, default, _eventFilter, _user));

				if (!await readNextSource.Task) {
					return false;
				}

				if (_buffer.TryDequeue(out current)) {
					_current = current;
					return true;
				}

				return false;

				void OnMessage(Message message) {
					if (message is ClientMessage.NotHandled notHandled &&
					    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						readNextSource.TrySetException(ex);
						return;
					}

					if (!(message is ClientMessage.ReadAllEventsBackwardFilteredCompleted completed)) {
						readNextSource.TrySetException(
							RpcExceptions.UnknownMessage<ClientMessage.ReadAllEventsBackwardFilteredCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case ReadAllFilteredResult.Success:
							foreach (var @event in completed.Events) {
								_buffer.Enqueue(@event);
							}

							_isEnd = completed.IsEndOfStream;
							_nextPosition = Position.FromInt64(
								completed.NextPos.CommitPosition,
								completed.NextPos.PreparePosition);
							readNextSource.TrySetResult(true);
							return;
						case ReadAllFilteredResult.AccessDenied:
							readNextSource.TrySetException(RpcExceptions.AccessDenied());
							return;
						default:
							readNextSource.TrySetException(RpcExceptions.UnknownError(completed.Result));
							return;
					}
				}
			}
		}
	}
}
