using System;
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
					new EventData(Uuid.NewUuid(), SystemEventTypes.Settings,
						JsonSerializer.SerializeToUtf8Bytes(settings, SystemSettingsJsonSerializerOptions))
				}, userCredentials, cancellationToken);
		}
	}
}
