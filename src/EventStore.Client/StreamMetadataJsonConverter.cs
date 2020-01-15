using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventStore.Client {
	public class StreamMetadataJsonConverter : JsonConverter<StreamMetadata> {
		public static readonly StreamMetadataJsonConverter Instance = new StreamMetadataJsonConverter();

		public override StreamMetadata Read(ref Utf8JsonReader reader, Type typeToConvert,
			JsonSerializerOptions options) {
			int? maxCount = default;
			TimeSpan? maxAge = default, cacheControl = default;
			StreamRevision? truncateBefore = default;
			StreamAcl acl = null;
			using var stream = new MemoryStream();
			using var customMetadataWriter = new Utf8JsonWriter(stream);

			if (reader.TokenType != JsonTokenType.StartObject) {
				throw new InvalidOperationException();
			}

			customMetadataWriter.WriteStartObject();

			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndObject) {
					break;
				}

				if (reader.TokenType != JsonTokenType.PropertyName) {
					throw new InvalidOperationException();
				}

				switch (reader.GetString()) {
					case SystemMetadata.MaxCount:
						if (!reader.Read()) {
							throw new InvalidOperationException();
						}

						maxCount = reader.GetInt32();
						break;
					case SystemMetadata.MaxAge:
						if (!reader.Read()) {
							throw new InvalidOperationException();
						}

						var int64 = reader.GetInt64();
						maxAge = TimeSpan.FromSeconds(int64);
						break;
					case SystemMetadata.CacheControl:
						if (!reader.Read()) {
							throw new InvalidOperationException();
						}

						cacheControl = TimeSpan.FromSeconds(reader.GetInt64());
						break;
					case SystemMetadata.TruncateBefore:
						if (!reader.Read()) {
							throw new InvalidOperationException();
						}

						var value = reader.GetInt64();
						truncateBefore = value == Int64.MaxValue
							? StreamRevision.End
							: StreamRevision.FromInt64(value);
						break;
					case SystemMetadata.Acl:
						if (!reader.Read()) {
							throw new InvalidOperationException();
						}

						acl = StreamAclJsonConverter.Instance.Read(ref reader, typeof(StreamAcl), options);
						break;
					default:
						customMetadataWriter.WritePropertyName(reader.GetString());
						reader.Read();
						switch (reader.TokenType) {
							case JsonTokenType.Comment:
								customMetadataWriter.WriteCommentValue(reader.GetComment());
								break;
							case JsonTokenType.String:
								customMetadataWriter.WriteStringValue(reader.GetString());
								break;
							case JsonTokenType.Number:
								customMetadataWriter.WriteNumberValue(reader.GetDouble());
								break;
							case JsonTokenType.True:
							case JsonTokenType.False:
								customMetadataWriter.WriteBooleanValue(reader.GetBoolean());
								break;
							case JsonTokenType.Null:
								reader.Read();
								customMetadataWriter.WriteNullValue();
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}

						break;
				}
			}

			customMetadataWriter.WriteEndObject();
			customMetadataWriter.Flush();

			stream.Position = 0;

			return new StreamMetadata(maxCount, maxAge, truncateBefore, cacheControl, acl,
				JsonDocument.Parse(stream));
		}

		public override void Write(Utf8JsonWriter writer, StreamMetadata value, JsonSerializerOptions options) {
			writer.WriteStartObject();

			if (value.MaxCount.HasValue) {
				writer.WriteNumber(SystemMetadata.MaxCount, value.MaxCount.Value);
			}

			if (value.MaxAge.HasValue) {
				writer.WriteNumber(SystemMetadata.MaxAge, (long)value.MaxAge.Value.TotalSeconds);
			}

			if (value.TruncateBefore.HasValue) {
				writer.WriteNumber(SystemMetadata.TruncateBefore, value.TruncateBefore.Value.ToInt64());
			}

			if (value.CacheControl.HasValue) {
				writer.WriteNumber(SystemMetadata.CacheControl, (long)value.CacheControl.Value.TotalSeconds);
			}

			if (value.Acl != null) {
				writer.WritePropertyName(SystemMetadata.Acl);
				StreamAclJsonConverter.Instance.Write(writer, value.Acl, options);
			}

			if (value.CustomMetadata != null) {
				foreach (var property in value.CustomMetadata.RootElement.EnumerateObject()) {
					property.WriteTo(writer);
				}
			}

			writer.WriteEndObject();
		}
	}
}
