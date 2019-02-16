using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.ClientAPI.Common.Utils;
using Newtonsoft.Json.Linq;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Builder for <see cref="StreamMetadata"/>.
	/// </summary>
	public class StreamMetadataBuilder {
		private long? _maxCount;
		private TimeSpan? _maxAge;
		private long? _truncateBefore;
		private TimeSpan? _cacheControl;
		private string[] _aclRead;
		private string[] _aclWrite;
		private string[] _aclDelete;
		private string[] _aclMetaRead;
		private string[] _aclMetaWrite;
		private readonly IDictionary<string, JToken> _customMetadata;

		/// <summary>
		/// All existing custom property keys.
		/// </summary>
		/// <remarks>
		/// Returns a copy of the key list so it's safe to enumerate whilst removing custom properties.
		/// </remarks>
		public IEnumerable<string> CustomPropertyKeys => _customMetadata.Keys.ToList();

		internal StreamMetadataBuilder(
			long? maxCount = null,
			TimeSpan? maxAge = null,
			long? truncateBefore = null,
			TimeSpan? cacheControl = null,
			string[] aclRead = null,
			string[] aclWrite = null,
			string[] aclDelete = null,
			string[] aclMetaRead = null,
			string[] aclMetaWrite = null,
			IDictionary<string, JToken> customMetadata = null) {
			_maxCount = maxCount;
			_maxAge = maxAge;
			_truncateBefore = truncateBefore;
			_cacheControl = cacheControl;
			_aclRead = aclRead;
			_aclWrite = aclWrite;
			_aclDelete = aclDelete;
			_aclMetaRead = aclMetaRead;
			_aclMetaWrite = aclMetaWrite;
			_customMetadata = customMetadata == null
				? new Dictionary<string, JToken>()
				: new Dictionary<string, JToken>(customMetadata);
		}

		/// <summary>
		/// Builds a <see cref="StreamMetadata"/> from a <see cref="StreamMetadataBuilder"/>.
		/// </summary>
		/// <param name="builder">A <see cref="StreamMetadataBuilder"/>.</param>
		/// <returns>A <see cref="StreamMetadata"/>.</returns>
		public static implicit operator StreamMetadata(StreamMetadataBuilder builder) {
			return builder.Build();
		}

		/// <summary>
		/// Builds a <see cref="StreamMetadata"/> from a <see cref="StreamMetadataBuilder"/>.
		/// </summary>
		public StreamMetadata Build() {
			var acl = this._aclRead == null
			          && this._aclWrite == null
			          && this._aclDelete == null
			          && this._aclMetaRead == null
			          && this._aclMetaWrite == null
				? null
				: new StreamAcl(this._aclRead, this._aclWrite, this._aclDelete, this._aclMetaRead, this._aclMetaWrite);
			return new StreamMetadata(this._maxCount, this._maxAge, this._truncateBefore, this._cacheControl, acl,
				this._customMetadata);
		}

		/// <summary>
		/// Sets the maximum number of events allowed in the stream.
		/// </summary>
		/// <param name="maxCount">The maximum number of events allowed in the stream.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetMaxCount(long maxCount) {
			Ensure.Positive(maxCount, "maxCount");
			_maxCount = maxCount;
			return this;
		}

		/// <summary>
		/// Sets the maximum age of events allowed in the stream.
		/// </summary>
		/// <param name="maxAge">The maximum age of events allowed in the stream.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetMaxAge(TimeSpan maxAge) {
			Ensure.Positive(maxAge.Ticks, "maxAge");
			_maxAge = maxAge;
			return this;
		}

		/// <summary>
		/// Sets the event number from which previous events can be scavenged.
		/// </summary>
		/// <param name="truncateBefore">The event number from which previous events can be scavenged.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetTruncateBefore(long truncateBefore) {
			Ensure.Nonnegative(truncateBefore, "truncateBefore");
			_truncateBefore = truncateBefore;
			return this;
		}

		/// <summary>
		/// Sets the amount of time for which the stream head is cachable.
		/// </summary>
		/// <param name="cacheControl">The amount of time for which the stream head is cachable.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCacheControl(TimeSpan cacheControl) {
			Ensure.Positive(cacheControl.Ticks, "cacheControl");
			_cacheControl = cacheControl;
			return this;
		}

		/// <summary>
		/// Sets a single role name with read permission for the stream.
		/// </summary>
		/// <param name="role">Role name.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetReadRole(string role) {
			_aclRead = role == null ? null : new[] {role};
			return this;
		}

		/// <summary>
		/// Sets role names with read permission for the stream.
		/// </summary>
		/// <param name="roles">Role names.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetReadRoles(string[] roles) {
			_aclRead = roles;
			return this;
		}

		/// <summary>
		/// Sets a single role name with write permission for the stream.
		/// </summary>
		/// <param name="role">Role name.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetWriteRole(string role) {
			_aclWrite = role == null ? null : new[] {role};
			return this;
		}

		/// <summary>
		/// Sets role names with write permission for the stream.
		/// </summary>
		/// <param name="roles">Role names.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetWriteRoles(string[] roles) {
			_aclWrite = roles;
			return this;
		}

		/// <summary>
		/// Sets a single role name with delete permission for the stream.
		/// </summary>
		/// <param name="role">Role name.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetDeleteRole(string role) {
			_aclDelete = role == null ? null : new[] {role};
			return this;
		}

		/// <summary>
		/// Sets role names with delete permission for the stream.
		/// </summary>
		/// <param name="roles">Role names.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetDeleteRoles(string[] roles) {
			_aclDelete = roles;
			return this;
		}

		/// <summary>
		/// Sets a single role name with metadata read permission for the stream.
		/// </summary>
		/// <param name="role">Role name.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetMetadataReadRole(string role) {
			_aclMetaRead = role == null ? null : new[] {role};
			return this;
		}

		/// <summary>
		/// Sets role names with metadata read permission for the stream.
		/// </summary>
		/// <param name="roles">Role names.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetMetadataReadRoles(string[] roles) {
			_aclMetaRead = roles;
			return this;
		}

		/// <summary>
		/// Sets a single role name with metadata write permission for the stream.
		/// </summary>
		/// <param name="role">Role name.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetMetadataWriteRole(string role) {
			_aclMetaWrite = role == null ? null : new[] {role};
			return this;
		}

		/// <summary>
		/// Sets role names with metadata write permission for the stream.
		/// </summary>
		/// <param name="roles">Role names.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetMetadataWriteRoles(string[] roles) {
			_aclMetaWrite = roles;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, string value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, int value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, int? value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, long value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, long? value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, float value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, float? value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, double value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, double? value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, decimal value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, decimal? value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, bool value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomProperty(string key, bool? value) {
			_customMetadata[key] = value;
			return this;
		}

		/// <summary>
		/// Sets a custom metadata property to a string of raw JSON.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="rawJson">The value.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder SetCustomPropertyWithValueAsRawJsonString(string key, string rawJson) {
			_customMetadata[key] = JToken.Parse(rawJson);
			return this;
		}

		/// <summary>
		/// Removes a custom property.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns>The builder.</returns>
		public StreamMetadataBuilder RemoveCustomProperty(string key) {
			_customMetadata.Remove(key);
			return this;
		}
	}
}
