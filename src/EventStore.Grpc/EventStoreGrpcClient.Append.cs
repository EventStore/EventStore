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
					Id = ByteString.CopyFrom(Uuid.NewUuid().ToSpan()),
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
					Id = ByteString.CopyFrom(Uuid.NewUuid().ToSpan()),
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), eventData, userCredentials, cancellationToken);

		private async Task<WriteResult> AppendToStreamInternal(
			AppendReq header,
			IEnumerable<EventData> eventData,
			UserCredentials userCredentials,
			CancellationToken cancellationToken) {
			using var call = _client.Append(RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);

			await call.RequestStream.WriteAsync(header);

			foreach (var e in eventData)
				await call.RequestStream.WriteAsync(new AppendReq {
					ProposedMessage = new AppendReq.Types.ProposedMessage {
						Id = ByteString.CopyFrom(e.EventId.ToSpan()),
						Data = ByteString.CopyFrom(e.Data),
						CustomMetadata = ByteString.CopyFrom(e.Metadata),
						Metadata = {
							{Constants.Metadata.Type, e.Type},
							{Constants.Metadata.IsJson, e.IsJson.ToString().ToLowerInvariant()}
						}
					}
				});

			await call.RequestStream.CompleteAsync();

			var response = await call.ResponseAsync;

			return new WriteResult(
				response.CurrentRevisionOptionsCase == AppendResp.CurrentRevisionOptionsOneofCase.NoStream
					? AnyStreamRevision.NoStream.ToInt64()
					: new StreamRevision(response.CurrentRevision).ToInt64(),
				response.PositionOptionsCase == AppendResp.PositionOptionsOneofCase.Position
					? new Position(response.Position.CommitPosition, response.Position.PreparePosition)
					: default);
		}
	}
}
