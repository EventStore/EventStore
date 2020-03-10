using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client;
using IEventFilter = EventStore.Core.Util.IEventFilter;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Enumerators {
		public class ReadAllForwardsFiltered : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly ulong _maxCount;
			private readonly IEventFilter _eventFilter;
			private readonly uint _maxSearchWindow;
			private readonly bool _resolveLinks;
			private readonly ClaimsPrincipal _user;
			private readonly bool _requiresLeader;
			private readonly Func<Position, Task> _checkpointReached;
			private readonly DateTime _deadline;
			private readonly CancellationTokenSource _disposedTokenSource;
			private readonly ConcurrentQueue<ResolvedEvent> _buffer;
			private readonly CancellationTokenRegistration _tokenRegistration;

			private Position _nextPosition;
			private bool _isEnd;
			private ResolvedEvent _current;
			private ulong _readCount;

			public ResolvedEvent Current => _current;

			public ReadAllForwardsFiltered(IPublisher bus,
				Position position,
				ulong maxCount,
				bool resolveLinks,
				IEventFilter eventFilter,
				ClaimsPrincipal user,
				bool requiresLeader,
				uint? maxSearchWindow,
				Func<Position, Task> checkpointReached,
				DateTime deadline,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (eventFilter == null) {
					throw new ArgumentNullException(nameof(eventFilter));
				}

				if (checkpointReached == null) {
					throw new ArgumentNullException(nameof(checkpointReached));
				}

				if (maxCount <= 0) {
					throw new ArgumentOutOfRangeException(nameof(maxCount));
				}

				_bus = bus;
				_nextPosition = position;
				_maxCount = maxCount;
				_maxSearchWindow = maxSearchWindow ?? 32;
				_eventFilter = eventFilter;
				_resolveLinks = resolveLinks;
				_user = user;
				_requiresLeader = requiresLeader;
				_checkpointReached = checkpointReached;
				_deadline = deadline;
				_disposedTokenSource = new CancellationTokenSource();
				_buffer = new ConcurrentQueue<ResolvedEvent>();
				_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);
			}

			public ValueTask DisposeAsync() {
				_disposedTokenSource.Dispose();
				_tokenRegistration.Dispose();
				return default;
			}

			public async ValueTask<bool> MoveNextAsync() {
				ReadLoop:
				if (_readCount >= _maxCount || _disposedTokenSource.IsCancellationRequested) {
					return false;
				}

				if (_buffer.TryDequeue(out var current)) {
					_current = current;
					_readCount++;
					return true;
				}

				if (_isEnd) {
					return false;
				}

				var readNextSource = new TaskCompletionSource<bool>();
				var checkpointReachedSource = new TaskCompletionSource<Position>();

				var correlationId = Guid.NewGuid();

				var (commitPosition, preparePosition) = _nextPosition.ToInt64();

				_bus.Publish(new ClientMessage.FilteredReadAllEventsForward(
					correlationId, correlationId, new CallbackEnvelope(OnMessage), commitPosition, preparePosition,
					Math.Min(32, (int)_maxCount), _resolveLinks, _requiresLeader, (int)_maxSearchWindow, default,
					_eventFilter, _user, expires: _deadline));

				var result = await Task.WhenAny(readNextSource.Task, checkpointReachedSource.Task)
					.ConfigureAwait(false);

				if (result == checkpointReachedSource.Task) {
					await _checkpointReached(checkpointReachedSource.Task.Result).ConfigureAwait(false);
					goto ReadLoop;
				}

				if (!await readNextSource.Task.ConfigureAwait(false)) {
					return false;
				}

				if (_disposedTokenSource.IsCancellationRequested) {
					return false;
				}

				if (_buffer.TryDequeue(out current)) {
					_current = current;
					_readCount++;
					return true;
				}

				goto ReadLoop;

				void OnMessage(Message message) {
					if (message is ClientMessage.NotHandled notHandled &&
					    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						readNextSource.TrySetException(ex);
						return;
					}

					if (!(message is ClientMessage.FilteredReadAllEventsForwardCompleted completed)) {
						readNextSource.TrySetException(
							RpcExceptions.UnknownMessage<ClientMessage.FilteredReadAllEventsForwardCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case FilteredReadAllResult.Success:
							foreach (var @event in completed.Events) {
								_buffer.Enqueue(@event);
							}

							_isEnd = completed.IsEndOfStream;
							_nextPosition = Position.FromInt64(
								completed.NextPos.CommitPosition,
								completed.NextPos.PreparePosition);

							if (!_isEnd && completed.Events.Length == 0) {
								checkpointReachedSource.TrySetResult(_nextPosition);
							} else {
								readNextSource.TrySetResult(true);
							}

							return;
						case FilteredReadAllResult.AccessDenied:
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
