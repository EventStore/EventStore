using System;
using System.IO;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents global settings for an Event Store server.
	/// </summary>
	public class SystemSettings {
		/// <summary>
		/// Default access control list for new user streams.
		/// </summary>
		public readonly StreamAcl UserStreamAcl;

		/// <summary>
		/// Default access control list for new system streams.
		/// </summary>
		public readonly StreamAcl SystemStreamAcl;

		/// <summary>
		/// Constructs a new <see cref="SystemSettings"/>.
		/// </summary>
		/// <param name="userStreamAcl"></param>
		/// <param name="systemStreamAcl"></param>
		public SystemSettings(StreamAcl userStreamAcl, StreamAcl systemStreamAcl) {
			UserStreamAcl = userStreamAcl;
			SystemStreamAcl = systemStreamAcl;
		}

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>
		/// A string that represents the current object.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		public override string ToString() {
			return string.Format("UserStreamAcl: ({0}), SystemStreamAcl: ({1})", UserStreamAcl, SystemStreamAcl);
		}

		/// <summary>
		/// Creates a <see cref="SystemSettings"/> object from a JSON string
		/// in a byte array.
		/// </summary>
		/// <param name="json">Byte array containing a JSON string.</param>
		/// <returns>A <see cref="SystemSettings"/> object.</returns>
		public static SystemSettings FromJsonBytes(byte[] json) {
			using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(json)))) {
				Check(reader.Read(), reader);
				Check(JsonToken.StartObject, reader);

				StreamAcl userStreamAcl = null;
				StreamAcl systemStreamAcl = null;

				while (true) {
					Check(reader.Read(), reader);
					if (reader.TokenType == JsonToken.EndObject)
						break;
					Check(JsonToken.PropertyName, reader);
					var name = (string)reader.Value;
					switch (name) {
						case SystemMetadata.UserStreamAcl:
							userStreamAcl = StreamMetadata.ReadAcl(reader);
							break;
						case SystemMetadata.SystemStreamAcl:
							systemStreamAcl = StreamMetadata.ReadAcl(reader);
							break;
						default: {
							Check(reader.Read(), reader);
							// skip
							JToken.ReadFrom(reader);
							break;
						}
					}
				}

				return new SystemSettings(userStreamAcl, systemStreamAcl);
			}
		}

		private static void Check(JsonToken type, JsonTextReader reader) {
			if (reader.TokenType != type)
				throw new Exception("Invalid JSON");
		}

		private static void Check(bool read, JsonTextReader reader) {
			if (!read)
				throw new Exception("Invalid JSON");
		}

		/// <summary>
		/// Creates a byte array containing a UTF-8 string with no byte order
		/// mark representing this <see cref="SystemSettings"/> object.
		/// </summary>
		/// <returns>A byte array containing a UTF-8 string with no byte order mark.</returns>
		public byte[] ToJsonBytes() {
			using (var memoryStream = new MemoryStream()) {
				using (var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream, Helper.UTF8NoBom))) {
					WriteAsJson(jsonWriter);
				}

				return memoryStream.ToArray();
			}
		}

		/// <summary>
		/// Creates a string containing representing this <see cref="SystemSettings"/>
		/// object.
		/// </summary>
		/// <returns>A string representing this <see cref="SystemSettings"/>.</returns>
		public string ToJsonString() {
			using (var stringWriter = new StringWriter()) {
				using (var jsonWriter = new JsonTextWriter(stringWriter)) {
					WriteAsJson(jsonWriter);
				}

				return stringWriter.ToString();
			}
		}

		private void WriteAsJson(JsonTextWriter jsonWriter) {
			jsonWriter.WriteStartObject();
			if (UserStreamAcl != null) {
				jsonWriter.WritePropertyName(SystemMetadata.UserStreamAcl);
				StreamMetadata.WriteAcl(jsonWriter, UserStreamAcl);
			}

			if (SystemStreamAcl != null) {
				jsonWriter.WritePropertyName(SystemMetadata.SystemStreamAcl);
				StreamMetadata.WriteAcl(jsonWriter, SystemStreamAcl);
			}

			jsonWriter.WriteEndObject();
		}
	}
}
