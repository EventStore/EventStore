using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Common;
using Serilog;
using IReadIndex = EventStore.Core.Services.Storage.ReaderIndex.IReadIndex;

namespace EventStore.Core.Services.Transport.Enumerators {
	partial class Enumerator {
		public class ReadAllBackwardsFiltered : IAsyncEnumerator<ReadResponse> {
			private static readonly ILogger Log = Serilog.Log.ForContext<ReadAllBackwardsFiltered>();

			private readonly IPublisher _bus;
			private readonly ulong _maxCount;
			private readonly bool _resolveLinks;
			private readonly IEventFilter _eventFilter;
			private readonly ClaimsPrincipal _user;
			private readonly bool _requiresLeader;
			private readonly IReadIndex _readIndex;
			private readonly DateTime _deadline;
			private readonly uint _maxSearchWindow;
			private readonly CancellationToken _cancellationToken;
			private readonly SemaphoreSlim _semaphore;
			private readonly Channel<ReadResponse> _channel;

			private ReadResponse _current;

			public ReadResponse Current => _current;

			public ReadAllBackwardsFiltered(IPublisher bus,
				Position position,
				ulong maxCount,
				bool resolveLinks,
				IEventFilter eventFilter,
				ClaimsPrincipal user,
				bool requiresLeader,
				IReadIndex readIndex,
				uint? maxSearchWindow,
				DateTime deadline,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (eventFilter == null) {
					throw new ArgumentNullException(nameof(eventFilter));
				}

				if (readIndex == null) {
					throw new ArgumentNullException(nameof(readIndex));
				}

				_bus = bus;
				_maxCount = maxCount;
				_resolveLinks = resolveLinks;
				_eventFilter = eventFilter;
				_user = user;
				_requiresLeader = requiresLeader;
				_readIndex = readIndex;
				_maxSearchWindow = maxSearchWindow ?? ReadBatchSize;
				_deadline = deadline;
				_cancellationToken = cancellationToken;
				_semaphore = new SemaphoreSlim(1, 1);
				_channel = Channel.CreateBounded<ReadResponse>(BoundedChannelOptions);

				ReadPage(position);
			}

			public ValueTask DisposeAsync() {
				_channel.Writer.TryComplete();
				return new ValueTask(Task.CompletedTask);
			}

			public async ValueTask<bool> MoveNextAsync() {
				if (!await _channel.Reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false)) {
					return false;
				}

				_current = await _channel.Reader.ReadAsync(_cancellationToken).ConfigureAwait(false);

				return true;
			}

			private void ReadPage(Position startPosition, ulong readCount = 0) {
				var correlationId = Guid.NewGuid();

				var (commitPosition, preparePosition) = startPosition.ToInt64();

				_bus.Publish(new ClientMessage.FilteredReadAllEventsBackward(
					correlationId, correlationId, new ContinuationEnvelope(OnMessage, _semaphore, _cancellationToken),
					commitPosition, preparePosition, (int)Math.Min(ReadBatchSize, _maxCount), _resolveLinks,
					_requiresLeader, (int)_maxSearchWindow, null, _eventFilter, _user, expires: _deadline,
					cancellationToken: _cancellationToken));

				async Task OnMessage(Message message, CancellationToken ct) {
					if (message is ClientMessage.NotHandled notHandled &&
					    TryHandleNotHandled(notHandled, out var ex)) {
						_channel.Writer.TryComplete(ex);
						return;
					}

					if (message is not ClientMessage.FilteredReadAllEventsBackwardCompleted completed) {
						_channel.Writer.TryComplete(
							ReadResponseException.UnknownMessage.Create<ClientMessage.FilteredReadAllEventsBackwardCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case FilteredReadAllResult.Success:
							foreach (var @event in completed.Events) {
								if (readCount >= _maxCount) {
									_channel.Writer.TryComplete();
									return;
								}
								await _channel.Writer.WriteAsync(new ReadResponse.EventReceived(@event), ct).ConfigureAwait(false);
								readCount++;
							}

							if (completed.IsEndOfStream) {
								_channel.Writer.TryComplete();
								return;
							}

							ReadPage(Position.FromInt64(
								completed.NextPos.CommitPosition,
								completed.NextPos.PreparePosition), readCount);
							return;
						case FilteredReadAllResult.AccessDenied:
							_channel.Writer.TryComplete(new ReadResponseException.AccessDenied());
							return;
						default:
							_channel.Writer.TryComplete(ReadResponseException.UnknownError.Create(completed.Result));
							return;
					}
				}
			}
		}
	}
}
