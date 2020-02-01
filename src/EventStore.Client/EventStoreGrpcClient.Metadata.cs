using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;

namespace EventStore.Client {
	public partial class EventStoreClient {
		public async Task<StreamMetadataResult> GetStreamMetadataAsync(string streamName,
			UserCredentials userCredentials = default,
			TimeSpan? timeoutAfter = default,
			CancellationToken cancellationToken = default) {
			ResolvedEvent metadata = default;

			try {
				metadata = await ReadStreamAsync(Direction.Backwards, SystemStreams.MetastreamOf(streamName),
					StreamRevision.End,
					1,
					false,
					userCredentials: userCredentials,
					timeoutAfter: timeoutAfter,
					cancellationToken: cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
			} catch (StreamNotFoundException) {
				return StreamMetadataResult.None(streamName);
			}

			return metadata.Event == null
				? StreamMetadataResult.None(streamName)
				: StreamMetadataResult.Create(
					streamName,
					metadata.OriginalEventNumber,
					JsonSerializer.Deserialize<StreamMetadata>(metadata.Event.Data, StreamMetadataJsonSerializerOptions));
		}

		public Task<WriteResult> SetStreamMetadataAsync(string streamName, AnyStreamRevision expectedRevision,
			StreamMetadata metadata, UserCredentials userCredentials = default,
			TimeSpan? timeoutAfter = default,
			CancellationToken cancellationToken = default)
			=> SetStreamMetadataInternal(metadata, new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = SystemStreams.MetastreamOf(streamName)
				}
			}.WithAnyStreamRevision(expectedRevision), userCredentials, timeoutAfter, cancellationToken);

		public Task<WriteResult> SetStreamMetadataAsync(string streamName, StreamRevision expectedRevision,
			StreamMetadata metadata, UserCredentials userCredentials = default,
			TimeSpan? timeoutAfter = default,
			CancellationToken cancellationToken = default)
			=> SetStreamMetadataInternal(metadata, new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = SystemStreams.MetastreamOf(streamName),
					Revision = expectedRevision
				}
			}, userCredentials, timeoutAfter, cancellationToken);

		private Task<WriteResult> SetStreamMetadataInternal(StreamMetadata metadata,
			AppendReq appendReq,
			UserCredentials userCredentials,
			TimeSpan? timeoutAfter,
			CancellationToken cancellationToken) =>
			AppendToStreamInternal(appendReq, new[] {
				new EventData(Uuid.NewUuid(), SystemEventTypes.StreamMetadata,
					JsonSerializer.SerializeToUtf8Bytes(metadata, StreamMetadataJsonSerializerOptions)),
			}, userCredentials, timeoutAfter, cancellationToken);
	}
}
