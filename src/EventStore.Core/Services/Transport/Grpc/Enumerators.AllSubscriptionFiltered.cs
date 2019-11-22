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
using IEventFilter = EventStore.Core.Util.IEventFilter;
using IReadIndex = EventStore.Core.Services.Storage.ReaderIndex.IReadIndex;

namespace EventStore.Core.Services.Transport.Grpc {
	internal static partial class Enumerators {
		public class AllSubscriptionFiltered : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly bool _resolveLinks;
			private readonly IEventFilter _eventFilter;
			private readonly IPrincipal _user;
			private readonly IReadIndex _readIndex;
			private readonly CancellationTokenSource _disposedTokenSource;
			private readonly ConcurrentQueue<ResolvedEvent> _buffer;

			private Position _nextPosition;
			private ResolvedEvent _current;

			public ResolvedEvent Current => _current;

			public AllSubscriptionFiltered(IPublisher bus,
				Position position,
				bool resolveLinks,
				IEventFilter eventFilter,
				IPrincipal user,
				IReadIndex readIndex,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (readIndex == null) {
					throw new ArgumentNullException(nameof(readIndex));
				}

				if (eventFilter == null) {
					throw new ArgumentNullException(nameof(eventFilter));
				}

				_bus = bus;
				_nextPosition = position;
				_resolveLinks = resolveLinks;
				_eventFilter = eventFilter;
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

				_bus.Publish(new ClientMessage.ReadAllEventsForwardFiltered(
					correlationId, correlationId, new CallbackEnvelope(OnMessage), commitPosition, preparePosition, 32,
					_resolveLinks, false, 32, default, _eventFilter, _user));

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

					if (!(message is ClientMessage.ReadAllEventsForwardFilteredCompleted completed)) {
						readNextSource.TrySetException(
							RpcExceptions.UnknownMessage<ClientMessage.ReadAllEventsForwardFilteredCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case ReadAllFilteredResult.Success:
							foreach (var @event in completed.Events) {
								_buffer.Enqueue(@event);
							}

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
