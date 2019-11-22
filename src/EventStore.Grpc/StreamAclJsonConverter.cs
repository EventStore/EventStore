using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventStore.Grpc {
	public class StreamAclJsonConverter : JsonConverter<StreamAcl> {
		public static readonly StreamAclJsonConverter Instance = new StreamAclJsonConverter();

		public override StreamAcl Read(ref Utf8JsonReader reader, Type typeToConvert,
			JsonSerializerOptions options) {
			string[] read = default,
				write = default,
				delete = default,
				metaRead = default,
				metaWrite = default;
			if (reader.TokenType != JsonTokenType.StartObject) {
				throw new InvalidOperationException();
			}

			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndObject) {
					break;
				}

				if (reader.TokenType != JsonTokenType.PropertyName) {
					throw new InvalidOperationException();
				}

				switch (reader.GetString()) {
					case SystemMetadata.AclRead:
						read = ReadRoles(ref reader);
						break;
					case SystemMetadata.AclWrite:
						write = ReadRoles(ref reader);
						break;
					case SystemMetadata.AclDelete:
						delete = ReadRoles(ref reader);
						break;
					case SystemMetadata.AclMetaRead:
						metaRead = ReadRoles(ref reader);
						break;
					case SystemMetadata.AclMetaWrite:
						metaWrite = ReadRoles(ref reader);
						break;
				}
			}

			return new StreamAcl(read, write, delete, metaRead, metaWrite);
		}

		private static string[] ReadRoles(ref Utf8JsonReader reader) {
			if (!reader.Read()) {
				throw new InvalidOperationException();
			}

			if (reader.TokenType == JsonTokenType.Null) {
				return null;
			}

			if (reader.TokenType == JsonTokenType.String) {
				return new[] {reader.GetString()};
			}

			if (reader.TokenType != JsonTokenType.StartArray) {
				throw new InvalidOperationException();
			}

			var roles = new List<string>();

			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndArray) {
					return roles.Count == 0 ? Array.Empty<string>() : roles.ToArray();
				}

				if (reader.TokenType != JsonTokenType.String) {
					throw new InvalidOperationException();
				}

				roles.Add(reader.GetString());
			}

			return roles.ToArray();
		}

		public override void Write(Utf8JsonWriter writer, StreamAcl value, JsonSerializerOptions options) {
			writer.WriteStartObject();

			WriteRoles(writer, SystemMetadata.AclRead, value.ReadRoles);
			WriteRoles(writer, SystemMetadata.AclWrite, value.WriteRoles);
			WriteRoles(writer, SystemMetadata.AclDelete, value.DeleteRoles);
			WriteRoles(writer, SystemMetadata.AclMetaRead, value.MetaReadRoles);
			WriteRoles(writer, SystemMetadata.AclMetaWrite, value.MetaWriteRoles);

			writer.WriteEndObject();
		}

		private static void WriteRoles(Utf8JsonWriter writer, string name, string[] roles) {
			if (roles == null) {
				return;
			}
			writer.WritePropertyName(name);
			writer.WriteStartArray();
			foreach (var role in roles) {
				writer.WriteStringValue(role);
			}

			writer.WriteEndArray();
		}
	}
}
