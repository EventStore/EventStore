using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventStore.Client {
	public class SystemSettingsJsonConverter : JsonConverter<SystemSettings> {
		public static readonly SystemSettingsJsonConverter Instance = new SystemSettingsJsonConverter();

		public override SystemSettings Read(ref Utf8JsonReader reader, Type typeToConvert,
			JsonSerializerOptions options) {
			if (reader.TokenType != JsonTokenType.StartObject) {
				throw new InvalidOperationException();
			}

			StreamAcl system = default, user = default;

			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndObject) {
					break;
				}

				if (reader.TokenType != JsonTokenType.PropertyName) {
					throw new InvalidOperationException();
				}

				switch (reader.GetString()) {
					case SystemMetadata.SystemStreamAcl:
						if (!reader.Read()) {
							throw new InvalidOperationException();
						}

						system = StreamAclJsonConverter.Instance.Read(ref reader, typeof(StreamAcl), options);
						break;
					case SystemMetadata.UserStreamAcl:
						if (!reader.Read()) {
							throw new InvalidOperationException();
						}

						user = StreamAclJsonConverter.Instance.Read(ref reader, typeof(StreamAcl), options);
						break;
				}
			}

			return new SystemSettings(user, system);
		}

		public override void Write(Utf8JsonWriter writer, SystemSettings value, JsonSerializerOptions options) {
			writer.WriteStartObject();
			if (value.UserStreamAcl != null) {
				writer.WritePropertyName(SystemMetadata.UserStreamAcl);
				StreamAclJsonConverter.Instance.Write(writer, value.UserStreamAcl, options);
			}

			if (value.SystemStreamAcl != null) {
				writer.WritePropertyName(SystemMetadata.SystemStreamAcl);
				StreamAclJsonConverter.Instance.Write(writer, value.SystemStreamAcl, options);
			}

			writer.WriteEndObject();
		}
	}
}
