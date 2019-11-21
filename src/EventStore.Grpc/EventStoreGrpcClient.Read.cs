using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Grpc.Streams;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace EventStore.Grpc {
	public partial class EventStoreGrpcClient {
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
			int maxCount,
			bool resolveLinkTos = false,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
					ResolveLinks = resolveLinkTos,
					All = new ReadReq.Types.Options.Types.AllOptions {
						Position = new ReadReq.Types.Options.Types.Position {
							CommitPosition = position.CommitPosition,
							PreparePosition = position.PreparePosition
						}
					},
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
			int maxCount,
			bool resolveLinkTos = false,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Backwards,
					ResolveLinks = resolveLinkTos,
					All = new ReadReq.Types.Options.Types.AllOptions {
						Position = new ReadReq.Types.Options.Types.Position {
							CommitPosition = position.CommitPosition,
							PreparePosition = position.PreparePosition
						}
					},
					Count = maxCount,
					Filter = GetFilterOptions(filter)
				}
			},
			userCredentials,
			cancellationToken);

		public IAsyncEnumerable<ResolvedEvent> ReadStreamForwardsAsync(
			string streamName,
			StreamRevision revision,
			int count,
			bool resolveLinkTos = false,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
					ResolveLinks = resolveLinkTos,
					Stream = new ReadReq.Types.Options.Types.StreamOptions {
						StreamName = streamName,
						Revision = revision
					},
					Count = count
				}
			},
			userCredentials,
			cancellationToken);

		public IAsyncEnumerable<ResolvedEvent> ReadStreamBackwardsAsync(
			string streamName,
			StreamRevision revision,
			int count,
			bool resolveLinkTos = false,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Backwards,
					ResolveLinks = resolveLinkTos,
					Stream = new ReadReq.Types.Options.Types.StreamOptions {
						StreamName = streamName,
						Revision = revision
					},
					Count = count
				}
			}, userCredentials,
			cancellationToken);

		private async IAsyncEnumerable<ResolvedEvent> ReadInternal(
			ReadReq request,
			UserCredentials userCredentials,
			[EnumeratorCancellation] CancellationToken cancellationToken) {
			if (request.Options.CountOptionsCase == ReadReq.Types.Options.CountOptionsOneofCase.Count &&
			    request.Options.Count <= 0) {
				throw new ArgumentOutOfRangeException("count");
			}

			if (request.Options.Filter == null) {
				request.Options.NoFilter = new ReadReq.Types.Empty();
			}

			using var call = _client.Read(
				request, RequestMetadata.Create(userCredentials),
				cancellationToken: cancellationToken);

			await foreach (var e in call.ResponseStream
				.ReadAllAsync(cancellationToken)
				.Select(ConvertToResolvedEvent)
				.WithCancellation(cancellationToken)) {
				yield return e;
			}

			ResolvedEvent ConvertToResolvedEvent(ReadResp response) =>
				new ResolvedEvent(
					ConvertToEventRecord(response.Event.Event),
					ConvertToEventRecord(response.Event.Link),
					response.Event.PositionCase switch {
						ReadResp.Types.ReadEvent.PositionOneofCase.CommitPosition => new Position(
							response.Event.CommitPosition, 0).ToInt64().commitPosition,
						ReadResp.Types.ReadEvent.PositionOneofCase.NoPosition => default,
						_ => throw new InvalidOperationException()
					});

			EventRecord ConvertToEventRecord(ReadResp.Types.ReadEvent.Types.RecordedEvent e) =>
				e == null
					? null
					: new EventRecord(
						e.StreamName,
						new Uuid(e.Id.ToByteArray()),
						new StreamRevision(e.StreamRevision),
						e.Metadata,
						e.Data.ToByteArray(),
						e.CustomMetadata.ToByteArray());
		}
	}
}
