using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using Grpc.Core;

namespace EventStore.Client {
	public partial class EventStoreClient {
		/// <summary>
		/// Asynchronously reads all events in the node forward (e.g. beginning to end).
		/// </summary>
		/// <param name="direction">The direction in which to read. <see cref="Direction"/></param>
		/// <param name="position">The position to start reading from.</param>
		/// <param name="maxCount">The maximum count to read.</param>
		/// <param name="operationOptions"><see cref="EventStoreClientOperationOptions" /> to perform the operation with.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="filter">The optional <see cref="IEventFilter"/> to apply.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		private IAsyncEnumerable<ResolvedEvent> ReadAllAsync(
			Direction direction,
			Position position,
			ulong maxCount,
			EventStoreClientOperationOptions operationOptions,
			bool resolveLinkTos = false,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = direction switch {
						Direction.Backwards => ReadReq.Types.Options.Types.ReadDirection.Backwards,
						Direction.Forwards => ReadReq.Types.Options.Types.ReadDirection.Forwards,
						_ => throw new InvalidOperationException()
					},
					ResolveLinks = resolveLinkTos,
					All = ReadReq.Types.Options.Types.AllOptions.FromPosition(position),
					Count = maxCount,
					Filter = GetFilterOptions(filter)
				}
			},
			operationOptions,
			userCredentials,
			cancellationToken);
		
		public IAsyncEnumerable<ResolvedEvent> ReadAllAsync(
			Direction direction,
			Position position,
			ulong maxCount,
			bool resolveLinkTos = false,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadAllAsync(direction, position, maxCount,
			_settings.OperationOptions, resolveLinkTos, filter, userCredentials, cancellationToken);

		public IAsyncEnumerable<ResolvedEvent> ReadAllAsync(
			Direction direction,
			Position position,
			ulong maxCount,
			Action<EventStoreClientOperationOptions> configureOperationOptions,
			bool resolveLinkTos = false,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {

			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions(operationOptions);
			
			return ReadAllAsync(direction, position, maxCount, operationOptions, resolveLinkTos, filter, userCredentials,
				cancellationToken);
		}

		private IAsyncEnumerable<ResolvedEvent> ReadStreamAsync(
			Direction direction,
			string streamName,
			StreamRevision revision,
			ulong count,
			EventStoreClientOperationOptions operationOptions,
			bool resolveLinkTos = false,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = direction switch {
						Direction.Backwards => ReadReq.Types.Options.Types.ReadDirection.Backwards,
						Direction.Forwards => ReadReq.Types.Options.Types.ReadDirection.Forwards,
						_ => throw new InvalidOperationException()
					},
					ResolveLinks = resolveLinkTos,
					Stream = ReadReq.Types.Options.Types.StreamOptions.FromStreamNameAndRevision(streamName, revision),
					Count = count
				}
			},
			operationOptions,
			userCredentials,
			cancellationToken);
			
		
		public IAsyncEnumerable<ResolvedEvent> ReadStreamAsync(
			Direction direction,
			string streamName,
			StreamRevision revision,
			ulong count,
			bool resolveLinkTos = false,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadStreamAsync(direction, streamName, revision, count,
			_settings.OperationOptions, resolveLinkTos, userCredentials, cancellationToken);

		public IAsyncEnumerable<ResolvedEvent> ReadStreamAsync(
			Direction direction,
			string streamName,
			StreamRevision revision,
			ulong count,
			Action<EventStoreClientOperationOptions> configureOperationOptions,
			bool resolveLinkTos = false,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {

			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions(operationOptions);
			
			return ReadStreamAsync(direction, streamName, revision, count, operationOptions, resolveLinkTos, userCredentials,
				cancellationToken);
		}

		private async IAsyncEnumerable<ResolvedEvent> ReadInternal(
			ReadReq request,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials,
			[EnumeratorCancellation] CancellationToken cancellationToken) {
			if (request.Options.CountOptionCase == ReadReq.Types.Options.CountOptionOneofCase.Count &&
			    request.Options.Count <= 0) {
				throw new ArgumentOutOfRangeException("count");
			}

			if (request.Options.Filter == null) {
				request.Options.NoFilter = new ReadReq.Types.Empty();
			}

			request.Options.UuidOption = new ReadReq.Types.Options.Types.UUIDOption
				{Structured = new ReadReq.Types.Empty()};

			using var call = _client.Read(
				request, RequestMetadata.Create(userCredentials),
				deadline: DeadLine.After(operationOptions.TimeoutAfter), cancellationToken);

			await foreach (var e in call.ResponseStream
				.ReadAllAsync(cancellationToken)
				.Select(ConvertToResolvedEvent)
				.WithCancellation(cancellationToken)
				.ConfigureAwait(false)) {
				yield return e;
			}

			ResolvedEvent ConvertToResolvedEvent(ReadResp response) =>
				new ResolvedEvent(
					ConvertToEventRecord(response.Event.Event),
					ConvertToEventRecord(response.Event.Link),
					response.Event.PositionCase switch {
						ReadResp.Types.ReadEvent.PositionOneofCase.CommitPosition => new Position(
							response.Event.CommitPosition, 0).ToInt64().commitPosition,
						ReadResp.Types.ReadEvent.PositionOneofCase.NoPosition => null,
						_ => throw new InvalidOperationException()
					});

			EventRecord ConvertToEventRecord(ReadResp.Types.ReadEvent.Types.RecordedEvent e) =>
				e == null
					? null
					: new EventRecord(
						e.StreamName,
						Uuid.FromDto(e.Id),
						new StreamRevision(e.StreamRevision),
						new Position(e.CommitPosition, e.PreparePosition),
						e.Metadata,
						e.Data.ToByteArray(),
						e.CustomMetadata.ToByteArray());
		}
	}
}
