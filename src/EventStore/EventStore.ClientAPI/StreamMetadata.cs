// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.ClientAPI
{
    public struct RawStreamMetadataResult
    {
        public readonly string Stream;
        public readonly bool IsStreamDeleted;
        public readonly int MetastreamVersion;
        public readonly byte[] StreamMetadata;

        public RawStreamMetadataResult(string stream, bool isStreamDeleted, int metastreamVersion, byte[] streamMetadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            Stream = stream;
            IsStreamDeleted = isStreamDeleted;
            MetastreamVersion = metastreamVersion;
            StreamMetadata = streamMetadata ?? Empty.ByteArray;
        }
    }

    public struct StreamMetadataResult
    {
        public readonly string Stream;
        public readonly bool IsStreamDeleted;
        public readonly int MetastreamVersion;
        public readonly StreamMetadata StreamMetadata;

        public StreamMetadataResult(string stream, bool isStreamDeleted, int metastreamVersion, StreamMetadata streamMetadata)
        {
            Ensure.NotNullOrEmpty(stream, "stream");

            Stream = stream;
            IsStreamDeleted = isStreamDeleted;
            MetastreamVersion = metastreamVersion;
            StreamMetadata = streamMetadata;
        }
    }

    public class StreamMetadata
    {
        private static readonly Encoding UTF8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public readonly int? MaxCount;
        public readonly TimeSpan? MaxAge;
        public readonly TimeSpan? CacheControl;

        public IEnumerable<string> CustomKeys { get { return _customMetadata.Keys; } }
        public IEnumerable<KeyValuePair<string, string>> CustomMetadataAsRawJsons
        {
            get { return _customMetadata.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString())); }
        } 

        private readonly IDictionary<string, JToken> _customMetadata;

        internal StreamMetadata(int? maxCount, TimeSpan? maxAge, TimeSpan? cacheControl, IDictionary<string, JToken> customMetadata = null)
        {
            if (maxCount.HasValue && maxCount <= 0) throw new ArgumentOutOfRangeException("maxCount", string.Format("{0} should be positive value.", SystemMetadata.MaxCount));
            if (maxAge.HasValue && maxAge.Value.Ticks <= 0) throw new ArgumentOutOfRangeException("maxAge", string.Format("{0} should be positive time span.", SystemMetadata.MaxAge));
            if (cacheControl.HasValue && cacheControl.Value.Ticks <= 0) throw new ArgumentOutOfRangeException("cacheControl", string.Format("{0} should be positive time span.", SystemMetadata.CacheControl));

            MaxCount = maxCount;
            MaxAge = maxAge;
            CacheControl = cacheControl;
            _customMetadata = customMetadata ?? Empty.CustomStreamMetadata;  
        }

        public static StreamMetadata Create(int? maxCount = null, TimeSpan? maxAge = null, TimeSpan? cacheControl = null)
        {
            return new StreamMetadata(maxCount, maxAge, cacheControl);
        }

        public static StreamMetadataBuilder WithBuilder()
        {
            return new StreamMetadataBuilder();
        }

        public T GetValue<T>(string key)
        {
            T res;
            if (!TryGetValue(key, out res))
                throw new ArgumentException(string.Format("No key '{0}' found in custom metadata.", key));
            return res;
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            Ensure.NotNull(key, "key");

            JToken token;
            if (!_customMetadata.TryGetValue(key, out token))
            {
                value = default(T);
                return false;
            }

            value = token.Value<T>();
            return true;
        }

        public string GetValueAsRawJsonString(string key)
        {
            string res;
            if (!TryGetValueAsRawJsonString(key, out res))
                throw new ArgumentException(string.Format("No key '{0}' found in custom metadata.", key));
            return res;
        }

        public bool TryGetValueAsRawJsonString(string key, out string value)
        {
            Ensure.NotNull(key, "key");

            JToken token;
            if (!_customMetadata.TryGetValue(key, out token))
            {
                value = default(string);
                return false;
            }

            value = token.ToString(Formatting.None);
            return true;
        }

        public byte[] AsJsonBytes()
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream, UTF8NoBom)))
                {
                    jsonWriter.WriteStartObject();
                    if (MaxCount.HasValue)
                    {
                        jsonWriter.WritePropertyName(SystemMetadata.MaxCount);
                        jsonWriter.WriteValue(MaxCount.Value);
                    }
                    if (MaxAge.HasValue)
                    {
                        jsonWriter.WritePropertyName(SystemMetadata.MaxAge);
                        jsonWriter.WriteValue((int)MaxAge.Value.TotalSeconds);
                    }
                    if (CacheControl.HasValue)
                    {
                        jsonWriter.WritePropertyName(SystemMetadata.CacheControl);
                        jsonWriter.WriteValue((int)CacheControl.Value.TotalSeconds);
                    }
                    foreach (var customMetadata in _customMetadata)
                    {
                        jsonWriter.WritePropertyName(customMetadata.Key);
                        customMetadata.Value.WriteTo(jsonWriter);
                    }
                    jsonWriter.WriteEndObject();
                }
                return new MemoryStream().ToArray();
            }
        }

        public static StreamMetadata FromJsonBytes(byte[] json)
        {
            using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(json))))
            {
                Check(reader.Read(), reader);
                Check(JsonToken.StartObject, reader);

                int? maxCount = null;
                TimeSpan? maxAge = null;
                TimeSpan? cacheControl = null;
                Dictionary<string, JToken> customMetadata = null;

                while (true)
                {
                    Check(reader.Read(), reader);
                    if (reader.TokenType == JsonToken.EndObject)
                        break;
                    Check(JsonToken.PropertyName, reader);
                    var name = (string) reader.Value;
                    switch (name)
                    {
                        case SystemMetadata.MaxCount:
                        {
                            Check(reader.Read(), reader);
                            Check(JsonToken.Integer, reader);
                            maxCount = (int) reader.Value;
                            break;
                        }
                        case SystemMetadata.MaxAge:
                        {
                            Check(reader.Read(), reader);
                            Check(JsonToken.Integer, reader);
                            maxAge = TimeSpan.FromSeconds((int)reader.Value);
                            break;
                        }
                        case SystemMetadata.CacheControl:
                        {
                            Check(reader.Read(), reader);
                            Check(JsonToken.Integer, reader);
                            cacheControl = TimeSpan.FromSeconds((int)reader.Value);
                            break;
                        }
                        default:
                        {
                            if (customMetadata == null)
                                customMetadata = new Dictionary<string, JToken>();
                            Check(reader.Read(), reader);
                            var jToken = JToken.ReadFrom(reader);
                            customMetadata.Add(name, jToken);
                            break;
                        }
                    }
                }
                return new StreamMetadata(maxCount, maxAge, cacheControl, customMetadata);
            }
        }

        private static void Check(JsonToken type, JsonTextReader reader)
        {
            if (reader.TokenType != type)
                throw new Exception("Invalid JSON");
        } 

        private static void Check(bool read, JsonTextReader reader)
        {
            if (!read)
                throw new Exception("Invalid JSON");
        }
    }

    public class StreamMetadataBuilder
    {
        private int? _maxCount;
        private TimeSpan? _maxAge;
        private TimeSpan? _cacheControl;

        private readonly IDictionary<string, JToken> _customMetadata = new Dictionary<string, JToken>();

        public static implicit operator StreamMetadata(StreamMetadataBuilder builder)
        {
            return new StreamMetadata(builder._maxCount, builder._maxAge, builder._cacheControl, builder._customMetadata);
        }

        public StreamMetadataBuilder SetMaxCount(int maxCount)
        {
            Ensure.Positive(maxCount, "maxCount");
            _maxCount = maxCount;
            return this;
        }

        public StreamMetadataBuilder SetMaxAge(TimeSpan maxAge)
        {
            Ensure.Positive(maxAge.Ticks, "maxAge");
            _maxAge = maxAge;
            return this;
        }

        public StreamMetadataBuilder SetCacheControl(TimeSpan cacheControl)
        {
            Ensure.Positive(cacheControl.Ticks, "cacheControl");
            _cacheControl = cacheControl;
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, string value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, int value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, int? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, long value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, long? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, float value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, float? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, double value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, double? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, decimal value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, decimal? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, bool value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomProperty(string key, bool? value)
        {
            _customMetadata.Add(key, value);
            return this;
        }

        public StreamMetadataBuilder SetCustomPropertyWithValueAsRawJsonString(string key, string rawJson)
        {
            _customMetadata.Add(key, JToken.Parse(rawJson));
            return this;
        }
    }
}
