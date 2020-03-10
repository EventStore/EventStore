using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Shared;
using EventStore.Client.Streams;
using Grpc.Core;

namespace EventStore.Client {
	public partial class EventStoreClient {
		private async IAsyncEnumerable<ResolvedEvent> ReadAllAsync(
			Direction direction,
			Position position,
			ulong maxCount,
			EventStoreClientOperationOptions operationOptions,
			bool resolveLinkTos = false,
			FilterOptions filterOptions = null,
			UserCredentials userCredentials = default,
			[EnumeratorCancellation] CancellationToken cancellationToken = default) {
			await foreach (var (confirmation, checkpoint, resolvedEvent) in ReadInternal(new ReadReq {
					Options = new ReadReq.Types.Options {
						ReadDirection = direction switch {
							Direction.Backwards => ReadReq.Types.Options.Types.ReadDirection.Backwards,
							Direction.Forwards => ReadReq.Types.Options.Types.ReadDirection.Forwards,
							_ => throw new InvalidOperationException()
						},
						ResolveLinks = resolveLinkTos,
						All = ReadReq.Types.Options.Types.AllOptions.FromPosition(position),
						Count = maxCount,
						Filter = GetFilterOptions(filterOptions)
					}
				},
				operationOptions,
				userCredentials,
				cancellationToken)) {
				if (confirmation != SubscriptionConfirmation.None) {
					continue;
				}

				if (checkpoint.HasValue && filterOptions?.CheckpointReached != null) {
					await filterOptions.CheckpointReached.Invoke(checkpoint.Value, cancellationToken)
						.ConfigureAwait(false);
					continue;
				}

				yield return resolvedEvent;
			}
		}

		/// <summary>
		/// Asynchronously reads all events.
		/// </summary>
		/// <param name="direction">The <see cref="Direction"/> in which to read.</param>
		/// <param name="position">The <see cref="Position"/> to start reading from.</param>
		/// <param name="maxCount">The maximum count to read.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="filterOptions">The optional <see cref="FilterOptions"/> to apply.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public IAsyncEnumerable<ResolvedEvent> ReadAllAsync(
			Direction direction,
			Position position,
			ulong maxCount,
			Action<EventStoreClientOperationOptions> configureOperationOptions = default,
			bool resolveLinkTos = false,
			FilterOptions filterOptions = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {

			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions?.Invoke(operationOptions);

			return ReadAllAsync(direction, position, maxCount, operationOptions, resolveLinkTos, filterOptions,
				userCredentials, cancellationToken);
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
			cancellationToken)
			.Where(x => x.Item1 == SubscriptionConfirmation.None && !x.Item2.HasValue)
			.Select(x => x.Item3);

		/// <summary>
		/// Asynchronously reads all the events from a stream.
		/// </summary>
		/// <param name="direction">The <see cref="Direction"/> in which to read.</param>
		/// <param name="streamName">The name of the stream to read.</param>
		/// <param name="revision">The <see cref="StreamRevision"/> to start reading from.</param>
		/// <param name="count">The number of events to read from the stream.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public IAsyncEnumerable<ResolvedEvent> ReadStreamAsync(
			Direction direction,
			string streamName,
			StreamRevision revision,
			ulong count,
			Action<EventStoreClientOperationOptions> configureOperationOptions = default,
			bool resolveLinkTos = false,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions?.Invoke(operationOptions);

			return ReadStreamAsync(direction, streamName, revision, count, operationOptions, resolveLinkTos,
				userCredentials, cancellationToken);
		}

		private async IAsyncEnumerable<(SubscriptionConfirmation, Position?, ResolvedEvent)> ReadInternal(
			ReadReq request,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials,
			[EnumeratorCancellation] CancellationToken cancellationToken) {
			if (request.Options.CountOptionCase == ReadReq.Types.Options.CountOptionOneofCase.Count &&
			    request.Options.Count <= 0) {
				throw new ArgumentOutOfRangeException("count");
			}

			if (request.Options.Filter == null) {
				request.Options.NoFilter = new Empty();
			}

			request.Options.UuidOption = new ReadReq.Types.Options.Types.UUIDOption
				{Structured = new Empty()};

			using var call = _client.Read(
				request, RequestMetadata.Create(userCredentials),
				deadline: DeadLine.After(operationOptions.TimeoutAfter), cancellationToken);

			await foreach (var e in call.ResponseStream
				.ReadAllAsync(cancellationToken)
				.Select(ConvertToItem)
				.WithCancellation(cancellationToken)
				.ConfigureAwait(false)) {
				yield return e;
			}

			(SubscriptionConfirmation, Position?, ResolvedEvent) ConvertToItem(ReadResp response) => response.ContentCase switch {
				ReadResp.ContentOneofCase.Confirmation => (
					new SubscriptionConfirmation(response.Confirmation.SubscriptionId), null, default),
				ReadResp.ContentOneofCase.Event => (SubscriptionConfirmation.None,
					null,
					ConvertToResolvedEvent(response.Event)),
				ReadResp.ContentOneofCase.Checkpoint => (SubscriptionConfirmation.None,
					new Position(response.Checkpoint.CommitPosition, response.Checkpoint.PreparePosition),
					default),
				_ => throw new InvalidOperationException()
			};

			ResolvedEvent ConvertToResolvedEvent(ReadResp.Types.ReadEvent readEvent) =>
				new ResolvedEvent(
					ConvertToEventRecord(readEvent.Event),
					ConvertToEventRecord(readEvent.Link),
					readEvent.PositionCase switch {
						ReadResp.Types.ReadEvent.PositionOneofCase.CommitPosition => new Position(
							readEvent.CommitPosition, 0).ToInt64().commitPosition,
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
