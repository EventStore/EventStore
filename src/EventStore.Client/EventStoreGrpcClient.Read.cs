using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace EventStore.Client {
	public partial class EventStoreClient {
		/// <summary>
		/// Asynchronously reads all events in the node forward (e.g. beginning to end).
		/// </summary>
		/// <param name="position">The position to start reading from.</param>
		/// <param name="maxCount">The maximum count to read.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="filter">The optional <see cref="IEventFilter"/> to apply.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public IAsyncEnumerable<ResolvedEvent> ReadAllForwardsAsync(
			Position position,
			ulong maxCount,
			bool resolveLinkTos = false,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
					ResolveLinks = resolveLinkTos,
					All = ReadReq.Types.Options.Types.AllOptions.FromPosition(position),
					Count = maxCount,
					Filter = GetFilterOptions(filter)
				}
			},
			userCredentials,
			cancellationToken);

		/// <summary>
		/// Asynchronously reads all events in the node backwards (e.g. end to beginning).
		/// </summary>
		/// <param name="position">The position to start reading from.</param>
		/// <param name="maxCount">The maximum count to read.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="filter">The optional <see cref="IEventFilter"/> to apply.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public IAsyncEnumerable<ResolvedEvent> ReadAllBackwardsAsync(
			Position position,
			ulong maxCount,
			bool resolveLinkTos = false,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Backwards,
					ResolveLinks = resolveLinkTos,
					All = ReadReq.Types.Options.Types.AllOptions.FromPosition(position),
					Count = maxCount,
					Filter = GetFilterOptions(filter)
				}
			},
			userCredentials,
			cancellationToken);

		public IAsyncEnumerable<ResolvedEvent> ReadStreamForwardsAsync(
			string streamName,
			StreamRevision revision,
			ulong count,
			bool resolveLinkTos = false,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
					ResolveLinks = resolveLinkTos,
					Stream = ReadReq.Types.Options.Types.StreamOptions.FromStreamNameAndRevision(streamName, revision),
					Count = count
				}
			},
			userCredentials,
			cancellationToken);

		public IAsyncEnumerable<ResolvedEvent> ReadStreamBackwardsAsync(
			string streamName,
			StreamRevision revision,
			ulong count,
			bool resolveLinkTos = false,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Backwards,
					ResolveLinks = resolveLinkTos,
					Stream = ReadReq.Types.Options.Types.StreamOptions.FromStreamNameAndRevision(streamName, revision),
					Count = count
				}
			}, userCredentials,
			cancellationToken);

		private async IAsyncEnumerable<ResolvedEvent> ReadInternal(
			ReadReq request,
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
				cancellationToken: cancellationToken);

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
