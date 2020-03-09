using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace EventStore.Client {
	public partial class EventStoreClient {
		private Task<WriteResult> AppendToStreamAsync(
			string streamName,
			StreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			_log.LogDebug("Append to stream - {streamName}@{expectedRevision}.", streamName, expectedRevision);

			return AppendToStreamInternal(new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, eventData, operationOptions, userCredentials, cancellationToken);
		}

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
			CancellationToken cancellationToken = default) {
			_log.LogDebug("Append to stream - {streamName}@{expectedRevision}.", streamName, expectedRevision);

			return AppendToStreamInternal(new AppendReq {
				Options = new AppendReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), eventData, operationOptions, userCredentials, cancellationToken);
		}

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

		private async Task<WriteResult> AppendToStreamInternal(
			AppendReq header,
			IEnumerable<EventData> eventData,
			EventStoreClientOperationOptions operationOptions,
			UserCredentials userCredentials,
			CancellationToken cancellationToken) {
			using var call = _client.Append(RequestMetadata.Create(userCredentials),
				deadline: DeadLine.After(operationOptions.TimeoutAfter), cancellationToken: cancellationToken);

			try {
				await call.RequestStream.WriteAsync(header).ConfigureAwait(false);

				foreach (var e in eventData) {
					_log.LogTrace("Appending event to stream - {streamName}@{eventId} {eventType}.",
						header.Options.StreamName, e.EventId, e.Type);
					await call.RequestStream.WriteAsync(new AppendReq {
						ProposedMessage = new AppendReq.Types.ProposedMessage {
							Id = e.EventId.ToDto(),
							Data = ByteString.CopyFrom(e.Data),
							CustomMetadata = ByteString.CopyFrom(e.Metadata),
							Metadata = {
								{Constants.Metadata.Type, e.Type},
								{Constants.Metadata.ContentType, e.ContentType}
							}
						}
					}).ConfigureAwait(false);
				}

				await call.RequestStream.CompleteAsync().ConfigureAwait(false);
			} catch (InvalidOperationException exc) {
				_log.LogTrace(exc, "Got InvalidOperationException when appending events to stream - {streamName}. This is perfectly normal if the connection was closed from the server-side.", header.Options.StreamName);
			} catch (RpcException exc) {
				_log.LogTrace(exc, "Got RpcException when appending events to stream - {streamName}. This is perfectly normal if the connection was closed from the server-side.", header.Options.StreamName);
			}

			var response = await call.ResponseAsync.ConfigureAwait(false);

			var writeResult = new WriteResult(
				response.CurrentRevisionOptionCase == AppendResp.CurrentRevisionOptionOneofCase.NoStream
					? AnyStreamRevision.NoStream.ToInt64()
					: new StreamRevision(response.CurrentRevision).ToInt64(),
				response.PositionOptionCase == AppendResp.PositionOptionOneofCase.Position
					? new Position(response.Position.CommitPosition, response.Position.PreparePosition)
					: default);

			_log.LogDebug("Append to stream succeeded - {streamName}@{logPosition}/{nextExpectedVersion}.",
				header.Options.StreamName, writeResult.LogPosition, writeResult.NextExpectedVersion);

			return writeResult;
		}
	}
}
