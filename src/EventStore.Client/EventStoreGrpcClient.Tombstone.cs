using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;

namespace EventStore.Client {
	public partial class EventStoreClient {
		private Task<DeleteResult> TombstoneAsync(
			string streamName,
			StreamRevision expectedRevision,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			TombstoneInternal(new TombstoneReq {
				Options = new TombstoneReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, operationOptions, userCredentials, cancellationToken);

		/// <summary>
		/// Tombstones a stream asynchronously. Note: Tombstoned streams can never be recreated.
		/// </summary>
		/// <param name="streamName">The name of the stream to tombstone.</param>
		/// <param name="expectedRevision">The expected <see cref="StreamRevision"/> of the stream being deleted.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<DeleteResult> TombstoneAsync(
			string streamName,
			StreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => TombstoneAsync(streamName, expectedRevision,
			_settings.OperationOptions, userCredentials, cancellationToken);

		/// <summary>
		/// Tombstones a stream asynchronously. Note: Tombstoned streams can never be recreated.
		/// </summary>
		/// <param name="streamName">The name of the stream to tombstone.</param>
		/// <param name="expectedRevision">The expected <see cref="StreamRevision"/> of the stream being deleted.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<DeleteResult> TombstoneAsync(
			string streamName,
			StreamRevision expectedRevision,
			Action<EventStoreClientOperationOptions> configureOperationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			
			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions(operationOptions);
			
			return TombstoneAsync(streamName, expectedRevision, operationOptions, userCredentials, cancellationToken);
		}

		private Task<DeleteResult> TombstoneAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			TombstoneInternal(new TombstoneReq {
				Options = new TombstoneReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), operationOptions, userCredentials, cancellationToken);

		/// <summary>
		/// Tombstones a stream asynchronously. Note: Tombstoned streams can never be recreated.
		/// </summary>
		/// <param name="streamName">The name of the stream to tombstone.</param>
		/// <param name="expectedRevision">The expected <see cref="AnyStreamRevision"/> of the stream being deleted.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<DeleteResult> TombstoneAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => TombstoneAsync(streamName, expectedRevision,
			_settings.OperationOptions, userCredentials, cancellationToken);

		/// <summary>
		/// Tombstones a stream asynchronously. Note: Tombstoned streams can never be recreated.
		/// </summary>
		/// <param name="streamName">The name of the stream to tombstone.</param>
		/// <param name="expectedRevision">The expected <see cref="AnyStreamRevision"/> of the stream being deleted.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<DeleteResult> TombstoneAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			Action<EventStoreClientOperationOptions> configureOperationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			
			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions(operationOptions);
			
			return TombstoneAsync(streamName, expectedRevision, operationOptions, userCredentials, cancellationToken);
		}

		private async Task<DeleteResult> TombstoneInternal(TombstoneReq request,
			EventStoreClientOperationOptions operationOptions, UserCredentials userCredentials,
			CancellationToken cancellationToken) {
			var result = await _client.TombstoneAsync(request, RequestMetadata.Create(userCredentials),
				deadline: DeadLine.After(operationOptions.TimeoutAfter), cancellationToken);

			return new DeleteResult(new Position(result.Position.CommitPosition, result.Position.PreparePosition));
		}
	}
}
