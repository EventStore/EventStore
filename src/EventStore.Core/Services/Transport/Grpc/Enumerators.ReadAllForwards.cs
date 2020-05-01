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

namespace EventStore.Core.Services.Transport.Grpc {
	internal static partial class Enumerators {
		public class ReadAllForwards : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly ulong _maxCount;
			private readonly bool _resolveLinks;
			private readonly ClaimsPrincipal _user;
			private readonly bool _requiresLeader;
			private readonly DateTime _deadline;
			private readonly CancellationTokenSource _disposedTokenSource;
			private readonly ConcurrentQueue<ResolvedEvent> _buffer;
			private readonly CancellationTokenRegistration _tokenRegistration;

			private Position _nextPosition;
			private bool _isEnd;
			private ResolvedEvent _current;
			private ulong _readCount;

			public ResolvedEvent Current => _current;

			public ReadAllForwards(IPublisher bus,
				Position position,
				ulong maxCount,
				bool resolveLinks,
				ClaimsPrincipal user,
				bool requiresLeader,
				DateTime deadline,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				_bus = bus;
				_nextPosition = position;
				_maxCount = maxCount;
				_resolveLinks = resolveLinks;
				_user = user;
				_requiresLeader = requiresLeader;
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

				var correlationId = Guid.NewGuid();

				var (commitPosition, preparePosition) = _nextPosition.ToInt64();

				_bus.Publish(new ClientMessage.ReadAllEventsForward(
					correlationId, correlationId, new CallbackEnvelope(OnMessage),
					commitPosition, preparePosition, (int)Math.Min(32, _maxCount),
					_resolveLinks, _requiresLeader, default, _user, expires: _deadline));

				if (_disposedTokenSource.IsCancellationRequested) {
					return false;
				}

				if (!await readNextSource.Task.ConfigureAwait(false)) {
					return false;
				}

				if (_buffer.TryDequeue(out current)) {
					_current = current;
					_readCount++;
					return true;
				}

				return false;

				void OnMessage(Message message) {
					if (message is ClientMessage.NotHandled notHandled &&
					    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						readNextSource.TrySetException(ex);
						return;
					}

					if (!(message is ClientMessage.ReadAllEventsForwardCompleted completed)) {
						readNextSource.TrySetException(
							RpcExceptions.UnknownMessage<ClientMessage.ReadAllEventsForwardCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case ReadAllResult.Success:
							foreach (var @event in completed.Events) {
								_buffer.Enqueue(@event);
							}

							_isEnd = completed.IsEndOfStream;
							_nextPosition = Position.FromInt64(
								completed.NextPos.CommitPosition,
								completed.NextPos.PreparePosition);
							readNextSource.TrySetResult(true);
							return;
						case ReadAllResult.AccessDenied:
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
