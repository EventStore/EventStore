using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Grpc {
	public static class EventStoreGrpcClientExtensions {
		private static readonly JsonSerializerOptions SystemSettingsJsonSerializerOptions = new JsonSerializerOptions {
			Converters = {
				SystemSettingsJsonConverter.Instance
			},
		};

		public static Task SetSystemSettingsAsync(
			this EventStoreGrpcClient client,
			SystemSettings settings,
			UserCredentials userCredentials = default, CancellationToken cancellationToken = default) {
			if (client == null) throw new ArgumentNullException(nameof(client));
			return client.AppendToStreamAsync(SystemStreams.SettingsStream, AnyStreamRevision.Any,
				new[] {
					new EventData(Guid.NewGuid(), SystemEventTypes.Settings,
						JsonSerializer.SerializeToUtf8Bytes(settings, SystemSettingsJsonSerializerOptions))
				}, userCredentials, cancellationToken);
		}

		public static async Task<ConditionalWriteResult> ConditionalAppendToStreamAsync(
			this EventStoreGrpcClient client,
			string streamName,
			StreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			if (client == null) throw new ArgumentNullException(nameof(client));
			try {
				var result = await client.AppendToStreamAsync(streamName, expectedRevision, eventData, userCredentials,
					cancellationToken);
				return ConditionalWriteResult.FromWriteResult(result);
			} catch (StreamDeletedException) {
				return ConditionalWriteResult.StreamDeleted;
			} catch (WrongExpectedVersionException ex) {
				return ConditionalWriteResult.FromWrongExpectedVersion(ex);
			}
		}

		public static async Task<ConditionalWriteResult> ConditionalAppendToStreamAsync(
			this EventStoreGrpcClient client,
			string streamName,
			AnyStreamRevision expectedRevision,
			IEnumerable<EventData> eventData,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			if (client == null) throw new ArgumentNullException(nameof(client));
			try {
				var result = await client.AppendToStreamAsync(streamName, expectedRevision, eventData, userCredentials,
					cancellationToken);
				return ConditionalWriteResult.FromWriteResult(result);
			} catch (StreamDeletedException) {
				return ConditionalWriteResult.StreamDeleted;
			} catch (WrongExpectedVersionException ex) {
				return ConditionalWriteResult.FromWrongExpectedVersion(ex);
			}
		}
	}
}
