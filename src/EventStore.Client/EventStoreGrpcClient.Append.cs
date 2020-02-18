using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using Google.Protobuf;

namespace EventStore.Client {
	public partial class EventStoreClient {
		/// <summary>
		/// Appends events asynchronously to a stream.
		/// </summary>
		/// <param name="streamName">The name of the stream to append events to.</param>
		/// <param name="expectedRevision">The expected <see cref="StreamRevision"/> of the stream to append to.</param>
		/// <param name="eventData">An <see cref="IEnumerable{EventData}"/> to append to the stream.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="userCredentials">The <see cref="UserCredentials"/> for the operation.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<WriteResult> AppendToStreamAsync(
			string streamName,
			StreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			Action<EventStoreClientOperationOptions> configureOperationOptions = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			
			var options = _settings.OperationOptions.Clone();
			configureOperationOptions?.Invoke(options);
			
			return AppendToStreamAsync(streamName, expectedRevision, eventData, options, userCredentials,
				cancellationToken);
		}

		private Task<WriteResult> AppendToStreamAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			AppendToStreamInternal(new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), eventData, operationOptions, userCredentials, cancellationToken);

		/// <summary>
		/// Appends events asynchronously to a stream.
		/// </summary>
		/// <param name="streamName">The name of the stream to append events to.</param>
		/// <param name="expectedRevision">The expected <see cref="AnyStreamRevision"/> of the stream to append to.</param>
		/// <param name="eventData">An <see cref="IEnumerable{EventData}"/> to append to the stream.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="userCredentials">The <see cref="UserCredentials"/> for the operation.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public Task<WriteResult> AppendToStreamAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			Action<EventStoreClientOperationOptions> configureOperationOptions = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			
			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions?.Invoke(operationOptions);
			
			return AppendToStreamAsync(streamName, expectedRevision, eventData, operationOptions, userCredentials,
				cancellationToken);
		}

		private Task<WriteResult> AppendToStreamAsync(
			string streamName,
			StreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			AppendToStreamInternal(new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, eventData, operationOptions, userCredentials, cancellationToken);

		private async Task<WriteResult> AppendToStreamInternal(
			AppendReq header,
			IEnumerable<EventData> eventData,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials,
			CancellationToken cancellationToken) {
			using var call = _client.Append(RequestMetadata.Create(userCredentials),
				deadline: DeadLine.After(operationOptions.TimeoutAfter), cancellationToken: cancellationToken);

			await call.RequestStream.WriteAsync(header).ConfigureAwait(false);

			foreach (var e in eventData)
				await call.RequestStream.WriteAsync(new AppendReq {
					ProposedMessage = new AppendReq.Types.ProposedMessage {
						Id = e.EventId.ToDto(),
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
