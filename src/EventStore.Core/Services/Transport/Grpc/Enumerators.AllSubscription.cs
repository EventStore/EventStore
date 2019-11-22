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
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Grpc;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Enumerators {
		public class AllSubscription : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly bool _resolveLinks;
			private readonly IPrincipal _user;
			private readonly IReadIndex _readIndex;
			private readonly CancellationTokenSource _disposedTokenSource;
			private readonly ConcurrentQueue<ResolvedEvent> _buffer;

			private Position _nextPosition;
			private ResolvedEvent _current;

			public ResolvedEvent Current => _current;

			public AllSubscription(IPublisher bus,
				Position position,
				bool resolveLinks,
				IPrincipal user,
				IReadIndex readIndex,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (readIndex == null) {
					throw new ArgumentNullException(nameof(readIndex));
				}

				_bus = bus;
				_nextPosition = position;
				_resolveLinks = resolveLinks;
				_user = user;
				_readIndex = readIndex;
				_disposedTokenSource = new CancellationTokenSource();
				_buffer = new ConcurrentQueue<ResolvedEvent>();
				cancellationToken.Register(_disposedTokenSource.Dispose);
			}

			public ValueTask DisposeAsync() {
				_disposedTokenSource.Dispose();
				return default;
			}

			public async ValueTask<bool> MoveNextAsync() {
				ReadLoop:
				if (_buffer.TryDequeue(out var current)) {
					_current = current;
					return true;
				}

				var correlationId = Guid.NewGuid();

				var readNextSource = new TaskCompletionSource<bool>();

				var (commitPosition, preparePosition) = _nextPosition == Position.End
					? (_readIndex.LastCommitPosition, _readIndex.LastReplicatedPosition)
					: _nextPosition.ToInt64();

				_bus.Publish(new ClientMessage.ReadAllEventsForward(
					correlationId, correlationId, new CallbackEnvelope(OnMessage), commitPosition, preparePosition, 32,
					_resolveLinks, false, default, _user));

				await readNextSource.Task;

				if (_buffer.TryDequeue(out current)) {
					_current = current;
					return true;
				}

				await Task.Delay(100, _disposedTokenSource.Token);

				goto ReadLoop;

				void OnMessage(Message message) {
					if (message is ClientMessage.NotHandled notHandled &&
					    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						readNextSource.TrySetException(ex);
						return;
					}

					if (!(message is ClientMessage.ReadAllEventsForwardCompleted completed)) {
						readNextSource.TrySetException(RpcExceptions.UnknownMessage<ClientMessage.ReadAllEventsForwardCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case ReadAllResult.Success:
							foreach (var @event in completed.Events) {
								_buffer.Enqueue(@event);
							}

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
