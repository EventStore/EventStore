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
using EventStore.Common.Utils;
using EventStore.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Data
{
    public class StreamMetadata
    {
        public static readonly StreamMetadata Empty = new StreamMetadata(null, null, null, null);

        public readonly int? MaxCount;
        public readonly TimeSpan? MaxAge;
        public readonly TimeSpan? CacheControl;
        public readonly StreamAcl Acl;

        public StreamMetadata(int? maxCount, TimeSpan? maxAge, TimeSpan? cacheControl, StreamAcl acl)
        {
            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException(
                    "maxCount", string.Format("{0} should be positive value.", SystemMetadata.MaxCount));
            if (maxAge <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    "maxAge", string.Format("{0} should be positive time span.", SystemMetadata.MaxAge));
            if (cacheControl <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    "cacheControl", string.Format("{0} should be positive time span.", SystemMetadata.CacheControl));

            MaxCount = maxCount;
            MaxAge = maxAge;
            CacheControl = cacheControl;
            Acl = acl;
        }

        public override string ToString()
        {
            return string.Format("MaxCount: {0}, MaxAge: {1}, CacheControl: {2}, Acl: {3}", MaxCount, MaxAge, CacheControl, Acl);
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
                StreamAcl acl = null;

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
                            maxCount = (int) (long) reader.Value;
                            break;
                        }
                        case SystemMetadata.MaxAge:
                        {
                            Check(reader.Read(), reader);
                            Check(JsonToken.Integer, reader);
                            maxAge = TimeSpan.FromSeconds((long) reader.Value);
                            break;
                        }
                        case SystemMetadata.CacheControl:
                        {
                            Check(reader.Read(), reader);
                            Check(JsonToken.Integer, reader);
                            cacheControl = TimeSpan.FromSeconds((long) reader.Value);
                            break;
                        }
                        case SystemMetadata.Acl:
                        {
                            acl = ReadAcl(reader);
                            break;
                        }
                        default:
                        {
                            Check(reader.Read(), reader);
                            // skip
                            JToken.ReadFrom(reader);
                            break;
                        }
                    }
                }
                return new StreamMetadata(maxCount > 0 ? maxCount : null,
                                          maxAge > TimeSpan.Zero ? maxAge : null,
                                          cacheControl > TimeSpan.Zero ? cacheControl : null,
                                          acl);
            }
        }

        internal static StreamAcl ReadAcl(JsonTextReader reader)
        {
            Check(reader.Read(), reader);
            Check(JsonToken.StartObject, reader);

            string[] read = null;
            string[] write = null;
            string[] delete = null;
            string[] metaRead = null;
            string[] metaWrite = null;

            while (true)
            {
                Check(reader.Read(), reader);
                if (reader.TokenType == JsonToken.EndObject)
                    break;
                Check(JsonToken.PropertyName, reader);
                var name = (string) reader.Value;
                switch (name)
                {
                    case SystemMetadata.AclRead: read = ReadRoles(reader); break;
                    case SystemMetadata.AclWrite: write = ReadRoles(reader); break;
                    case SystemMetadata.AclDelete: delete = ReadRoles(reader); break;
                    case SystemMetadata.AclMetaRead: metaRead = ReadRoles(reader); break;
                    case SystemMetadata.AclMetaWrite: metaWrite = ReadRoles(reader); break;
                }
            }
            return new StreamAcl(read, write, delete, metaRead, metaWrite);
        }

        private static string[] ReadRoles(JsonTextReader reader)
        {
            Check(reader.Read(), reader);
            if (reader.TokenType == JsonToken.String)
                return new[] {(string) reader.Value};

            if (reader.TokenType == JsonToken.StartArray)
            {
                var roles = new List<string>();
                while (true)
                {
                    Check(reader.Read(), reader);
                    if (reader.TokenType == JsonToken.EndArray)
                        break;
                    Check(JsonToken.String, reader);
                    roles.Add((string) reader.Value);
                }
                return roles.ToArray();
            }

            throw new Exception("Invalid JSON");
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

        public byte[] ToJsonBytes()
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var jsonWriter = new JsonTextWriter(new StreamWriter(memoryStream, Helper.UTF8NoBom)))
                {
                    WriteAsJson(jsonWriter);
                }
                return memoryStream.ToArray();
            }
        }

        public string ToJsonString()
        {
            using (var stringWriter = new StringWriter())
            {
                using (var jsonWriter = new JsonTextWriter(stringWriter))
                {
                    WriteAsJson(jsonWriter);
                }
                return stringWriter.ToString();
            }
        }

        private void WriteAsJson(JsonTextWriter jsonWriter)
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
                jsonWriter.WriteValue((long) MaxAge.Value.TotalSeconds);
            }
            if (CacheControl.HasValue)
            {
                jsonWriter.WritePropertyName(SystemMetadata.CacheControl);
                jsonWriter.WriteValue((long) CacheControl.Value.TotalSeconds);
            }
            if (Acl != null)
            {
                jsonWriter.WritePropertyName(SystemMetadata.Acl);
                WriteAcl(jsonWriter, Acl);
            }
            jsonWriter.WriteEndObject();
        }

        internal static void WriteAcl(JsonTextWriter jsonWriter, StreamAcl acl)
        {
            jsonWriter.WriteStartObject();
            WriteAclRoles(jsonWriter, SystemMetadata.AclRead, acl.ReadRoles);
            WriteAclRoles(jsonWriter, SystemMetadata.AclWrite, acl.WriteRoles);
            WriteAclRoles(jsonWriter, SystemMetadata.AclDelete, acl.DeleteRoles);
            WriteAclRoles(jsonWriter, SystemMetadata.AclMetaRead, acl.MetaReadRoles);
            WriteAclRoles(jsonWriter, SystemMetadata.AclMetaWrite, acl.MetaWriteRoles);
            jsonWriter.WriteEndObject();
        }

        private static void WriteAclRoles(JsonTextWriter jsonWriter, string propertyName, string[] roles)
        {
            if (roles == null)
                return;
            jsonWriter.WritePropertyName(propertyName);
            if (roles.Length == 1)
            {
                jsonWriter.WriteValue(roles[0]);
            }
            else
            {
                jsonWriter.WriteStartArray();
                Array.ForEach(roles, jsonWriter.WriteValue);
                jsonWriter.WriteEndArray();
            }
        }
    }
}