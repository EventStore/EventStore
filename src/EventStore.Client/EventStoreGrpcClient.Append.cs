using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using Google.Protobuf;

namespace EventStore.Client {
	public partial class EventStoreClient {
		public Task<WriteResult> AppendToStreamAsync(
			string streamName,
			StreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			UserCredentials userCredentials = default,
			TimeSpan? timeoutAfter = default,
			CancellationToken cancellationToken = default) =>
			AppendToStreamInternal(new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, eventData, userCredentials, timeoutAfter, cancellationToken);

		public Task<WriteResult> AppendToStreamAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			UserCredentials userCredentials = default,
			TimeSpan? timeoutAfter = default,
			CancellationToken cancellationToken = default) =>
			AppendToStreamInternal(new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), eventData, userCredentials, timeoutAfter, cancellationToken);

		private async Task<WriteResult> AppendToStreamInternal(
			AppendReq header,
			IEnumerable<EventData> eventData,
			UserCredentials userCredentials,
			TimeSpan? timeoutAfter,
			CancellationToken cancellationToken) {
			using var call = _client.Append(RequestMetadata.Create(userCredentials),
				deadline: DeadLine.After(timeoutAfter), cancellationToken: cancellationToken);

			await call.RequestStream.WriteAsync(header).ConfigureAwait(false);

			foreach (var e in eventData)
				await call.RequestStream.WriteAsync(new AppendReq {
					ProposedMessage = new AppendReq.Types.ProposedMessage {
						Id = e.EventId.ToStreamsDto(),
						Data = ByteString.CopyFrom(e.Data),
						CustomMetadata = ByteString.CopyFrom(e.Metadata),
						Metadata = {
							{Constants.Metadata.Type, e.Type},
							{Constants.Metadata.IsJson, e.IsJson.ToString().ToLowerInvariant()}
						}
					}
				}).ConfigureAwait(false);

			await call.RequestStream.CompleteAsync().ConfigureAwait(false);

			var response = await call.ResponseAsync.ConfigureAwait(false);

			return new WriteResult(
				response.CurrentRevisionOptionCase == AppendResp.CurrentRevisionOptionOneofCase.NoStream
					? AnyStreamRevision.NoStream.ToInt64()
					: new StreamRevision(response.CurrentRevision).ToInt64(),
				response.PositionOptionCase == AppendResp.PositionOptionOneofCase.Position
					? new Position(response.Position.CommitPosition, response.Position.PreparePosition)
					: default);
		}
	}
}
