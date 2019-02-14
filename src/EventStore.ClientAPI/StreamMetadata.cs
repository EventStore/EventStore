using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EventStore.ClientAPI.Internal;

namespace EventStore.ClientAPI {
	/// <summary>
	/// A class representing stream metadata with strongly typed properties
	/// for system values and a dictionary-like interface for custom values.
	/// </summary>
	public class StreamMetadata {
		/// <summary>
		/// The maximum number of events allowed in the stream.
		/// </summary>
		public readonly long? MaxCount;

		/// <summary>
		/// The maximum age of events allowed in the stream.
		/// </summary>
		public readonly TimeSpan? MaxAge;

		/// <summary>
		/// The event number from which previous events can be scavenged.
		/// This is used to implement soft-deletion of streams.
		/// </summary>
		public readonly long? TruncateBefore;

		/// <summary>
		/// The amount of time for which the stream head is cachable.
		/// </summary>
		public readonly TimeSpan? CacheControl;

		/// <summary>
		/// The access control list for the stream.
		/// </summary>
		public readonly StreamAcl Acl;

		/// <summary>
		/// An enumerable of the keys in the user-provided metadata.
		/// </summary>
		public IEnumerable<string> CustomKeys {
			get { return _customMetadata.Keys; }
		}

		/// <summary>
		/// An enumerable of key-value pairs of keys to JSON text for user-provider metadata.
		/// </summary>
		public IEnumerable<KeyValuePair<string, string>> CustomMetadataAsRawJsons {
			get { return _customMetadata.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString())); }
		}

		private readonly IDictionary<string, JToken> _customMetadata;

		internal StreamMetadata(long? maxCount, TimeSpan? maxAge, long? truncateBefore, TimeSpan? cacheControl,
			StreamAcl acl, IDictionary<string, JToken> customMetadata = null) {
			if (maxCount <= 0)
				throw new ArgumentOutOfRangeException("maxCount",
					string.Format("{0} should be positive value.", SystemMetadata.MaxCount));
			if (maxAge <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException("maxAge",
					string.Format("{0} should be positive time span.", SystemMetadata.MaxAge));
			if (truncateBefore < 0)
				throw new ArgumentOutOfRangeException("truncateBefore",
					string.Format("{0} should be non-negative value.", SystemMetadata.TruncateBefore));

			if (cacheControl <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException("cacheControl",
					string.Format("{0} should be positive time span.", SystemMetadata.CacheControl));

			MaxCount = maxCount;
			MaxAge = maxAge;
			TruncateBefore = truncateBefore;
			CacheControl = cacheControl;
			Acl = acl;
			_customMetadata = customMetadata ?? Empty.CustomStreamMetadata;
		}

		/// <summary>
		/// Creates a <see cref="StreamMetadata" /> with the specified parameters.
		/// </summary>
		/// <param name="maxCount">The maximum number of events allowed in the stream.</param>
		/// <param name="maxAge">The maximum age of events allowed in the stream.</param>
		/// <param name="truncateBefore">The event number from which previous events can be scavenged.</param>
		/// <param name="cacheControl">The amount of time for which the stream head is cachable.</param>
		/// <param name="acl">The access control list for the stream.</param>
		/// <returns></returns>
		public static StreamMetadata Create(long? maxCount = null, TimeSpan? maxAge = null, long? truncateBefore = null,
			TimeSpan? cacheControl = null, StreamAcl acl = null) {
			return new StreamMetadata(maxCount, maxAge, truncateBefore, cacheControl, acl);
		}

		/// <summary>
		/// Creates a <see cref="StreamMetadataBuilder" /> for building a new <see cref="StreamMetadata"/>.
		/// </summary>
		/// <returns>An instance of <see cref="StreamMetadataBuilder"/>.</returns>
		public static StreamMetadataBuilder Build() {
			return new StreamMetadataBuilder();
		}

		/// <summary>
		/// Creates a <see cref="StreamMetadataBuilder" /> initialized with the values of this <see cref="StreamMetadata"/>
		/// </summary>
		/// <returns>An instance of <see cref="StreamMetadataBuilder"/>.</returns>
		public StreamMetadataBuilder Copy() {
			if (Acl == null)
				return new StreamMetadataBuilder(
					MaxCount,
					MaxAge,
					TruncateBefore,
					CacheControl,
					customMetadata: _customMetadata);
			return new StreamMetadataBuilder(
				MaxCount,
				MaxAge,
				TruncateBefore,
				CacheControl,
				Acl.ReadRoles,
				Acl.WriteRoles,
				Acl.DeleteRoles,
				Acl.MetaReadRoles,
				Acl.MetaWriteRoles,
				_customMetadata);
		}

		/// <summary>
		/// Get a value of type T for the given key from the custom metadata.
		/// This method will throw an <see cref="ArgumentException"/> if the
		/// key is not found.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="key">A key.</param>
		/// <returns>Value of type T for the key.</returns>
		public T GetValue<T>(string key) {
			T res;
			if (!TryGetValue(key, out res))
				throw new ArgumentException(string.Format("Key '{0}' not found in custom metadata.", key));
			return res;
		}

		/// <summary>
		/// Tries to get a value of type T for the given key from the custom
		/// metadata, and if it exists returns true from the method and gives
		/// the value as an out parameter.
		/// </summary>
		/// <param name="key">A key.</param>
		/// <param name="value">Output variable for the value of type T for the key.</param>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <returns>True if the key exists, false otherwise.</returns>
		public bool TryGetValue<T>(string key, out T value) {
			Ensure.NotNull(key, "key");

			JToken token;
			if (!_customMetadata.TryGetValue(key, out token)) {
				value = default(T);
				return false;
			}

			value = token.Value<T>();
			return true;
		}

		/// <summary>
		/// Gets a string containing raw JSON value for the given key.
		/// </summary>
		/// <param name="key">A key.</param>
		/// <returns>String containing raw JSON value for the key.</returns>
		/// <exception cref="ArgumentException">If the key does not exist.</exception>
		public string GetValueAsRawJsonString(string key) {
			string res;
			if (!TryGetValueAsRawJsonString(key, out res))
				throw new ArgumentException(string.Format("No key '{0}' found in custom metadata.", key));
			return res;
		}

		/// <summary>
		/// Tries to get a string containing raw JSON value for the given key.
		/// </summary>
		/// <param name="key">A key.</param>
		/// <param name="value">Output variable for the value for the key.</param>
		/// <returns>True if the key exists, false otherwise.</returns>
		public bool TryGetValueAsRawJsonString(string key, out string value) {
			Ensure.NotNull(key, "key");

			JToken token;
			if (!_customMetadata.TryGetValue(key, out token)) {
				value = default(string);
				return false;
			}

			value = token.ToString(Formatting.None);
			return true;
		}

		/// <summary>
		/// Returns a byte array representing the stream metadata
		/// as JSON encoded as UTF8 with no byte order mark.
		/// </summary>
		/// <returns>Byte array representing the stream metadata.</returns>
		public byte[] AsJsonBytes() {
			using (var memoryStream = new MemoryStream()) {
				using (var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream, Helper.UTF8NoBom))) {
					WriteAsJson(jsonWriter);
				}

				return memoryStream.ToArray();
			}
		}

		/// <summary>
		/// Returns a JSON string representing the stream metadata.
		/// </summary>
		/// <returns>A string representing the stream metadata.</returns>
		public string AsJsonString() {
			using (var stringWriter = new StringWriter()) {
				using (var jsonWriter = new JsonTextWriter(stringWriter)) {
					WriteAsJson(jsonWriter);
				}

				return stringWriter.ToString();
			}
		}

		private void WriteAsJson(JsonTextWriter jsonWriter) {
			jsonWriter.WriteStartObject();
			if (MaxCount.HasValue) {
				jsonWriter.WritePropertyName(SystemMetadata.MaxCount);
				jsonWriter.WriteValue(MaxCount.Value);
			}

			if (MaxAge.HasValue) {
				jsonWriter.WritePropertyName(SystemMetadata.MaxAge);
				jsonWriter.WriteValue((long)MaxAge.Value.TotalSeconds);
			}

			if (TruncateBefore.HasValue) {
				jsonWriter.WritePropertyName(SystemMetadata.TruncateBefore);
				jsonWriter.WriteValue(TruncateBefore.Value);
			}

			if (CacheControl.HasValue) {
				jsonWriter.WritePropertyName(SystemMetadata.CacheControl);
				jsonWriter.WriteValue((long)CacheControl.Value.TotalSeconds);
			}

			if (Acl != null) {
				jsonWriter.WritePropertyName(SystemMetadata.Acl);
				WriteAcl(jsonWriter, Acl);
			}

			foreach (var customMetadata in _customMetadata) {
				jsonWriter.WritePropertyName(customMetadata.Key);
				customMetadata.Value.WriteTo(jsonWriter);
			}

			jsonWriter.WriteEndObject();
		}

		internal static void WriteAcl(JsonTextWriter jsonWriter, StreamAcl acl) {
			jsonWriter.WriteStartObject();
			WriteAclRoles(jsonWriter, SystemMetadata.AclRead, acl.ReadRoles);
			WriteAclRoles(jsonWriter, SystemMetadata.AclWrite, acl.WriteRoles);
			WriteAclRoles(jsonWriter, SystemMetadata.AclDelete, acl.DeleteRoles);
			WriteAclRoles(jsonWriter, SystemMetadata.AclMetaRead, acl.MetaReadRoles);
			WriteAclRoles(jsonWriter, SystemMetadata.AclMetaWrite, acl.MetaWriteRoles);
			jsonWriter.WriteEndObject();
		}

		private static void WriteAclRoles(JsonTextWriter jsonWriter, string propertyName, string[] roles) {
			if (roles == null)
				return;
			jsonWriter.WritePropertyName(propertyName);
			if (roles.Length == 1) {
				jsonWriter.WriteValue(roles[0]);
			} else {
				jsonWriter.WriteStartArray();
				Array.ForEach(roles, jsonWriter.WriteValue);
				jsonWriter.WriteEndArray();
			}
		}

		/// <summary>
		/// Builds a <see cref="StreamMetadata" /> object from a byte array
		/// containing stream metadata.
		/// </summary>
		/// <param name="json"></param>
		/// <returns></returns>
		public static StreamMetadata FromJsonBytes(byte[] json) {
			using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(json)))) {
				Check(reader.Read(), reader);
				Check(JsonToken.StartObject, reader);

				long? maxCount = null;
				TimeSpan? maxAge = null;
				long? truncateBefore = null;
				TimeSpan? cacheControl = null;
				StreamAcl acl = null;
				Dictionary<string, JToken> customMetadata = null;

				while (true) {
					Check(reader.Read(), reader);
					if (reader.TokenType == JsonToken.EndObject)
						break;
					Check(JsonToken.PropertyName, reader);
					var name = (string)reader.Value;
					switch (name) {
						case SystemMetadata.MaxCount: {
							Check(reader.Read(), reader);
							Check(JsonToken.Integer, reader);
							maxCount = (long)reader.Value;
							break;
						}
						case SystemMetadata.MaxAge: {
							Check(reader.Read(), reader);
							Check(JsonToken.Integer, reader);
							maxAge = TimeSpan.FromSeconds((long)reader.Value);
							break;
						}
						case SystemMetadata.TruncateBefore: {
							Check(reader.Read(), reader);
							Check(JsonToken.Integer, reader);
							truncateBefore = (long)reader.Value;
							break;
						}
						case SystemMetadata.CacheControl: {
							Check(reader.Read(), reader);
							Check(JsonToken.Integer, reader);
							cacheControl = TimeSpan.FromSeconds((long)reader.Value);
							break;
						}
						case SystemMetadata.Acl: {
							acl = ReadAcl(reader);
							break;
						}
						default: {
							if (customMetadata == null)
								customMetadata = new Dictionary<string, JToken>();
							Check(reader.Read(), reader);
							var jToken = JToken.ReadFrom(reader);
							customMetadata.Add(name, jToken);
							break;
						}
					}
				}

				return new StreamMetadata(maxCount, maxAge, truncateBefore, cacheControl, acl, customMetadata);
			}
		}

		internal static StreamAcl ReadAcl(JsonTextReader reader) {
			Check(reader.Read(), reader);
			Check(JsonToken.StartObject, reader);

			string[] read = null;
			string[] write = null;
			string[] delete = null;
			string[] metaRead = null;
			string[] metaWrite = null;

			while (true) {
				Check(reader.Read(), reader);
				if (reader.TokenType == JsonToken.EndObject)
					break;
				Check(JsonToken.PropertyName, reader);
				var name = (string)reader.Value;
				switch (name) {
					case SystemMetadata.AclRead:
						read = ReadRoles(reader);
						break;
					case SystemMetadata.AclWrite:
						write = ReadRoles(reader);
						break;
					case SystemMetadata.AclDelete:
						delete = ReadRoles(reader);
						break;
					case SystemMetadata.AclMetaRead:
						metaRead = ReadRoles(reader);
						break;
					case SystemMetadata.AclMetaWrite:
						metaWrite = ReadRoles(reader);
						break;
				}
			}

			return new StreamAcl(read, write, delete, metaRead, metaWrite);
		}

		private static string[] ReadRoles(JsonTextReader reader) {
			Check(reader.Read(), reader);
			if (reader.TokenType == JsonToken.String)
				return new[] {(string)reader.Value};

			if (reader.TokenType == JsonToken.StartArray) {
				var roles = new List<string>();
				while (true) {
					Check(reader.Read(), reader);
					if (reader.TokenType == JsonToken.EndArray)
						break;
					Check(JsonToken.String, reader);
					roles.Add((string)reader.Value);
				}

				return roles.ToArray();
			}

			throw new Exception("Invalid JSON");
		}

		private static void Check(JsonToken type, JsonTextReader reader) {
			if (reader.TokenType != type)
				throw new Exception("Invalid JSON");
		}

		private static void Check(bool read, JsonTextReader reader) {
			if (!read)
				throw new Exception("Invalid JSON");
		}
	}
}
