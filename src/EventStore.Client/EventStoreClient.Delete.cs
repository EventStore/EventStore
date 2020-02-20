using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using Microsoft.Extensions.Logging;

namespace EventStore.Client {
	public partial class EventStoreClient {
		/// <summary>
		/// Soft Deletes a stream asynchronously.
		/// </summary>
		/// <param name="streamName">The name of the stream to delete.</param>
		/// <param name="expectedRevision">The expected <see cref="StreamRevision"/> of the stream being deleted.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<DeleteResult> SoftDeleteAsync(
			string streamName,
			StreamRevision expectedRevision,
			Action<EventStoreClientOperationOptions> configureOperationOptions = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {

			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions?.Invoke(operationOptions);

			return SoftDeleteAsync(streamName, expectedRevision, operationOptions, userCredentials, cancellationToken);
		}

		/// <summary>
		/// Soft Deletes a stream asynchronously.
		/// </summary>
		/// <param name="streamName">The name of the stream to delete.</param>
		/// <param name="expectedRevision">The expected <see cref="AnyStreamRevision"/> of the stream being deleted.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="userCredentials">The optional <see cref="UserCredentials"/> to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<DeleteResult> SoftDeleteAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			Action<EventStoreClientOperationOptions> configureOperationOptions = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			
			var options = _settings.OperationOptions.Clone();
			configureOperationOptions?.Invoke(options);

			return SoftDeleteAsync(streamName, expectedRevision, options, userCredentials, cancellationToken);
		}

		private Task<DeleteResult> SoftDeleteAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			DeleteInternal(new DeleteReq {
				Options = new DeleteReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), operationOptions, userCredentials, cancellationToken);

		private Task<DeleteResult> SoftDeleteAsync(
			string streamName,
			StreamRevision expectedRevision,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			DeleteInternal(new DeleteReq {
				Options = new DeleteReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, operationOptions, userCredentials, cancellationToken);

		private async Task<DeleteResult> DeleteInternal(DeleteReq request, EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials,
			CancellationToken cancellationToken) {
			_log.LogDebug("Deleting stream {streamName}.", request.Options.StreamName);
			var result = await _client.DeleteAsync(request, RequestMetadata.Create(userCredentials),
				deadline: DeadLine.After(operationOptions.TimeoutAfter), cancellationToken);

			return new DeleteResult(new Position(result.Position.CommitPosition, result.Position.PreparePosition));
		}
	}
}
