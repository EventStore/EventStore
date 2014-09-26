using System;
using System.Collections.Generic;
using EventStore.ClientAPI.Common.Utils;
using Newtonsoft.Json.Linq;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Builder for <see cref="StreamMetadata"/>.
    /// </summary>
    public class StreamMetadataBuilder
    {
        private int? _maxCount;
        private TimeSpan? _maxAge;
        private int? _truncateBefore;
        private TimeSpan? _cacheControl;
        private string[] _aclRead;
        private string[] _aclWrite;
        private string[] _aclDelete;
        private string[] _aclMetaRead;
        private string[] _aclMetaWrite;

        private readonly IDictionary<string, JToken> _customMetadata = new Dictionary<string, JToken>();

        internal StreamMetadataBuilder()
        {
        }

        /// <summary>
        /// Builds a <see cref="StreamMetadata"/> from a <see cref="StreamMetadataBuilder"/>.
        /// </summary>
        /// <param name="builder">A <see cref="StreamMetadataBuilder"/>.</param>
        /// <returns>A <see cref="StreamMetadata"/>.</returns>
        public static implicit operator StreamMetadata(StreamMetadataBuilder builder)
        {
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
            return new StreamMetadata(this._maxCount, this._maxAge, this._truncateBefore, this._cacheControl, acl, this._customMetadata);
        }

        /// <summary>
        /// Sets the maximum number of events allowed in the stream.
        /// </summary>
        /// <param name="maxCount">The maximum number of events allowed in the stream.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetMaxCount(int maxCount)
        {
            Ensure.Positive(maxCount, "maxCount");
            _maxCount = maxCount;
            return this;
        }

        /// <summary>
        /// Sets the maximum age of events allowed in the stream.
        /// </summary>
        /// <param name="maxAge">The maximum age of events allowed in the stream.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetMaxAge(TimeSpan maxAge)
        {
            Ensure.Positive(maxAge.Ticks, "maxAge");
            _maxAge = maxAge;
            return this;
        }

        /// <summary>
        /// Sets the event number from which previous events can be scavenged.
        /// </summary>
        /// <param name="truncateBefore">The event number from which previous events can be scavenged.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetTruncateBefore(int truncateBefore)
        {
            Ensure.Nonnegative(truncateBefore, "truncateBefore");
            _truncateBefore = truncateBefore;
            return this;
        }

        /// <summary>
        /// Sets the amount of time for which the stream head is cachable.
        /// </summary>
        /// <param name="cacheControl">The amount of time for which the stream head is cachable.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCacheControl(TimeSpan cacheControl)
        {
            Ensure.Positive(cacheControl.Ticks, "cacheControl");
            _cacheControl = cacheControl;
            return this;
        }

        /// <summary>
        /// Sets a single role name with read permission for the stream.
        /// </summary>
        /// <param name="role">Role name.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetReadRole(string role)
        {
            _aclRead = role == null ? null : new[] { role };
            return this;
        }

        /// <summary>
        /// Sets role names with read permission for the stream.
        /// </summary>
        /// <param name="roles">Role names.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetReadRoles(string[] roles)
        {
            _aclRead = roles;
            return this;
        }

        /// <summary>
        /// Sets a single role name with write permission for the stream.
        /// </summary>
        /// <param name="role">Role name.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetWriteRole(string role)
        {
            _aclWrite = role == null ? null : new[] { role };
            return this;
        }

        /// <summary>
        /// Sets role names with write permission for the stream.
        /// </summary>
        /// <param name="roles">Role names.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetWriteRoles(string[] roles)
        {
            _aclWrite = roles;
            return this;
        }

        /// <summary>
        /// Sets a single role name with delete permission for the stream.
        /// </summary>
        /// <param name="role">Role name.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetDeleteRole(string role)
        {
            _aclDelete = role == null ? null : new[] { role };
            return this;
        }

        /// <summary>
        /// Sets role names with delete permission for the stream.
        /// </summary>
        /// <param name="roles">Role names.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetDeleteRoles(string[] roles)
        {
            _aclDelete = roles;
            return this;
        }

        /// <summary>
        /// Sets a single role name with metadata read permission for the stream.
        /// </summary>
        /// <param name="role">Role name.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetMetadataReadRole(string role)
        {
            _aclMetaRead = role == null ? null : new[] { role };
            return this;
        }

        /// <summary>
        /// Sets role names with metadata read permission for the stream.
        /// </summary>
        /// <param name="roles">Role names.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetMetadataReadRoles(string[] roles)
        {
            _aclMetaRead = roles;
            return this;
        }

        /// <summary>
        /// Sets a single role name with metadata write permission for the stream.
        /// </summary>
        /// <param name="role">Role name.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetMetadataWriteRole(string role)
        {
            _aclMetaWrite = role == null ? null : new[] { role };
            return this;
        }

        /// <summary>
        /// Sets role names with metadata write permission for the stream.
        /// </summary>
        /// <param name="roles">Role names.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetMetadataWriteRoles(string[] roles)
        {
            _aclMetaWrite = roles;
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, string value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, int value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, int? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, long value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, long? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, float value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, float? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, double value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, double? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, decimal value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, decimal? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, bool value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomProperty(string key, bool? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        /// <summary>
        /// Sets a custom metadata property to a string of raw JSON.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="rawJson">The value.</param>
        /// <returns>The builder.</returns>
        public StreamMetadataBuilder SetCustomPropertyWithValueAsRawJsonString(string key, string rawJson)
        {
            _customMetadata.Add(key, JToken.Parse(rawJson));
            return this;
        }
    }
}