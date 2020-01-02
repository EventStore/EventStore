using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Grpc.Streams;
using Google.Protobuf;

namespace EventStore.Grpc {
	public partial class EventStoreGrpcClient {
		public Task<WriteResult> AppendToStreamAsync(
			string streamName,
			StreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			AppendToStreamInternal(new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, eventData, userCredentials, cancellationToken);

		public Task<WriteResult> AppendToStreamAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			AppendToStreamInternal(new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), eventData, userCredentials, cancellationToken);

		private async Task<WriteResult> AppendToStreamInternal(
			AppendReq header,
			IEnumerable<EventData> eventData,
			UserCredentials userCredentials,
			CancellationToken cancellationToken) {
			using var call = _client.Append(RequestMetadata.Create(userCredentials),
				cancellationToken: cancellationToken);

			await call.RequestStream.WriteAsync(header).ConfigureAwait(false);

			foreach (var e in eventData)
				await call.RequestStream.WriteAsync(new AppendReq {
					ProposedMessage = new AppendReq.Types.ProposedMessage {
						Id = new Streams.UUID {
							String = e.EventId.ToString("n")
						},
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
