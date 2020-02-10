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

		/// <summary>
		/// Asynchronously reads the metadata for a stream
		/// </summary>
		/// <param name="streamName">The name of the stream to read the metadata for.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<StreamMetadataResult> GetStreamMetadataAsync(string streamName,
			UserCredentials userCredentials = default, CancellationToken cancellationToken = default)
			=> GetStreamMetadataAsync(streamName, _settings.OperationOptions, userCredentials, cancellationToken);

		/// <summary>
		/// Asynchronously reads the metadata for a stream
		/// </summary>
		/// <param name="streamName">The name of the stream to read the metadata for.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
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

		/// <summary>
		/// Asynchronously sets the metadata for a stream.
		/// </summary>
		/// <param name="streamName">The name of the stream to set metadata for.</param>
		/// <param name="expectedRevision">The <see cref="AnyStreamRevision"/> of the stream to append to.</param>
		/// <param name="metadata">A <see cref="StreamMetadata"/> representing the new metadata.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<WriteResult> SetStreamMetadataAsync(string streamName, AnyStreamRevision expectedRevision,
			StreamMetadata metadata, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default)
			=> SetStreamMetadataAsync(streamName, expectedRevision, metadata, _settings.OperationOptions,
				userCredentials, cancellationToken);

		/// <summary>
		/// Asynchronously sets the metadata for a stream.
		/// </summary>
		/// <param name="streamName">The name of the stream to set metadata for.</param>
		/// <param name="expectedRevision">The <see cref="AnyStreamRevision"/> of the stream to append to.</param>
		/// <param name="metadata">A <see cref="StreamMetadata"/> representing the new metadata.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
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
		
		/// <summary>
		/// Asynchronously sets the metadata for a stream.
		/// </summary>
		/// <param name="streamName">The name of the stream to set metadata for.</param>
		/// <param name="expectedRevision">The <see cref="StreamRevision"/> of the stream to append to.</param>
		/// <param name="metadata">A <see cref="StreamMetadata"/> representing the new metadata.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<WriteResult> SetStreamMetadataAsync(string streamName, StreamRevision expectedRevision,
			StreamMetadata metadata, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default)
			=> SetStreamMetadataAsync(streamName, expectedRevision, metadata, _settings.OperationOptions,
				userCredentials, cancellationToken);

		/// <summary>
		/// Asynchronously sets the metadata for a stream.
		/// </summary>
		/// <param name="streamName">The name of the stream to set metadata for.</param>
		/// <param name="expectedRevision">The <see cref="StreamRevision"/> of the stream to append to.</param>
		/// <param name="metadata">A <see cref="StreamMetadata"/> representing the new metadata.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
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
