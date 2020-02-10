using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;

namespace EventStore.Client {
	public partial class EventStoreClient {
		private async Task<StreamMetadataResult> GetStreamMetadataAsync(string streamName,
			EventStoreClientOperationOptions operationOptions, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			ResolvedEvent metadata = default;

			try {
				metadata = await ReadStreamAsync(Direction.Backwards, SystemStreams.MetastreamOf(streamName), StreamRevision.End, 1,
					operationOptions,
					resolveLinkTos: false,
					userCredentials: userCredentials,
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

		public Task<StreamMetadataResult> GetStreamMetadataAsync(string streamName,
			UserCredentials userCredentials = default, CancellationToken cancellationToken = default)
			=> GetStreamMetadataAsync(streamName, _settings.OperationOptions, userCredentials, cancellationToken);

		public Task<StreamMetadataResult> GetStreamMetadataAsync(string streamName,
			Action<EventStoreClientOperationOptions> configureOperationOptions,
			UserCredentials userCredentials = default, CancellationToken cancellationToken = default) {
			var options = _settings.OperationOptions.Clone();
			configureOperationOptions(options);
			return GetStreamMetadataAsync(streamName, options, userCredentials, cancellationToken);
		}

		private Task<WriteResult> SetStreamMetadataAsync(string streamName, AnyStreamRevision expectedRevision,
			StreamMetadata metadata, EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials = default, CancellationToken cancellationToken = default)
			=> SetStreamMetadataInternal(metadata, new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = SystemStreams.MetastreamOf(streamName)
				}
			}.WithAnyStreamRevision(expectedRevision), operationOptions, userCredentials, cancellationToken);

		public Task<WriteResult> SetStreamMetadataAsync(string streamName, AnyStreamRevision expectedRevision,
			StreamMetadata metadata, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default)
			=> SetStreamMetadataAsync(streamName, expectedRevision, metadata, _settings.OperationOptions,
				userCredentials, cancellationToken);

		public Task<WriteResult> SetStreamMetadataAsync(string streamName, AnyStreamRevision expectedRevision,
			StreamMetadata metadata, Action<EventStoreClientOperationOptions> configureOperationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			var options = _settings.OperationOptions.Clone();
			configureOperationOptions(options);
			return SetStreamMetadataAsync(streamName, expectedRevision, metadata, options,
				userCredentials, cancellationToken);
		}

		private Task<WriteResult> SetStreamMetadataAsync(string streamName, StreamRevision expectedRevision,
			StreamMetadata metadata, EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials = default, CancellationToken cancellationToken = default)
			=> SetStreamMetadataInternal(metadata, new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = SystemStreams.MetastreamOf(streamName),
					Revision = expectedRevision
				}
			}, operationOptions, userCredentials, cancellationToken);
		
		public Task<WriteResult> SetStreamMetadataAsync(string streamName, StreamRevision expectedRevision,
			StreamMetadata metadata, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default)
			=> SetStreamMetadataAsync(streamName, expectedRevision, metadata, _settings.OperationOptions,
				userCredentials, cancellationToken);

		public Task<WriteResult> SetStreamMetadataAsync(string streamName, StreamRevision expectedRevision,
			StreamMetadata metadata, Action<EventStoreClientOperationOptions> configureOperationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			var options = _settings.OperationOptions.Clone();
			configureOperationOptions(options);
			return SetStreamMetadataAsync(streamName, expectedRevision, metadata, options,
				userCredentials, cancellationToken);
		}

		private Task<WriteResult> SetStreamMetadataInternal(StreamMetadata metadata,
			AppendReq appendReq,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials,
			CancellationToken cancellationToken) =>
			AppendToStreamInternal(appendReq, new[] {
				new EventData(Uuid.NewUuid(), SystemEventTypes.StreamMetadata,
					JsonSerializer.SerializeToUtf8Bytes(metadata, StreamMetadataJsonSerializerOptions)),
			}, operationOptions, userCredentials, cancellationToken);
	}
}
