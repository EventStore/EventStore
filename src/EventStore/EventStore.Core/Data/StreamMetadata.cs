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
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Data
{
    public class StreamMetadata
    {
        public static readonly StreamMetadata Empty = new StreamMetadata(null, null, null, null, null);

        public readonly int? MaxCount;
        public readonly TimeSpan? MaxAge;
        public readonly int? StartFrom;
        public readonly TimeSpan? CacheControl;
        public readonly StreamAcl Acl;

        public StreamMetadata(int? maxCount, TimeSpan? maxAge, int? startFrom, TimeSpan? cacheControl, StreamAcl acl)
        {
            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException(
                    "maxCount", string.Format("{0} should be positive value.", SystemMetadata.MaxCount));
            if (maxAge <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    "maxAge", string.Format("{0} should be positive time span.", SystemMetadata.MaxAge));
            if (startFrom < 0)
                throw new ArgumentOutOfRangeException(
                    "startFrom", string.Format("{0} should be non negative value.", SystemMetadata.StartFrom));

            if (cacheControl <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(
                    "cacheControl", string.Format("{0} should be positive time span.", SystemMetadata.CacheControl));

            MaxCount = maxCount;
            MaxAge = maxAge;
            StartFrom = startFrom;
            CacheControl = cacheControl;
            Acl = acl;
        }

        public override string ToString()
        {
            return string.Format("MaxCount: {0}, MaxAge: {1}, StartFrom: {2} CacheControl: {3}, Acl: {4}", MaxCount, MaxAge, StartFrom, CacheControl, Acl);
        }

        public static StreamMetadata FromJsonBytes(byte[] json)
        {
            using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(json))))
            {
                Check(reader.Read(), reader);
                Check(JsonToken.StartObject, reader);

                int? maxCount = null;
                TimeSpan? maxAge = null;
                int? startFrom = null;
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
                        case SystemMetadata.StartFrom:
                        {
                            Check(reader.Read(), reader);
                            Check(JsonToken.Integer, reader);
                            startFrom = (int)(long)reader.Value;
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
                return new StreamMetadata(
                    maxCount > 0 ? maxCount : null, maxAge > TimeSpan.Zero ? maxAge : null,
                    startFrom >= 0 ? startFrom : null, cacheControl > TimeSpan.Zero ? cacheControl : null, acl);
            }
        }

        private static StreamAcl ReadAcl(JsonTextReader reader)
        {
            Check(reader.Read(), reader);
            Check(JsonToken.StartObject, reader);

            string read = null;
            string write = null;
            string delete = null;
            string metaRead = null;
            string metaWrite = null;

            while (true)
            {
                Check(reader.Read(), reader);
                if (reader.TokenType == JsonToken.EndObject)
                    break;
                Check(JsonToken.PropertyName, reader);
                var name = (string) reader.Value;
                switch (name)
                {
                    case SystemMetadata.AclRead:
                    {
                        Check(reader.Read(), reader);
                        Check(JsonToken.String, reader);
                        read = (string) reader.Value;
                        break;
                    }
                    case SystemMetadata.AclWrite:
                    {
                        Check(reader.Read(), reader);
                        Check(JsonToken.String, reader);
                        write = (string)reader.Value;
                        break;
                    }
                    case SystemMetadata.AclDelete:
                    {
                        Check(reader.Read(), reader);
                        Check(JsonToken.String, reader);
                        delete = (string)reader.Value;
                        break;
                    }
                    case SystemMetadata.AclMetaRead:
                    {
                        Check(reader.Read(), reader);
                        Check(JsonToken.String, reader);
                        metaRead = (string) reader.Value;
                        break;
                    }
                    case SystemMetadata.AclMetaWrite:
                    {
                        Check(reader.Read(), reader);
                        Check(JsonToken.String, reader);
                        metaWrite = (string) reader.Value;
                        break;
                    }
                }
            }
            return new StreamAcl(read, write, delete, metaRead, metaWrite);
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
            if (StartFrom.HasValue)
            {
                jsonWriter.WritePropertyName(SystemMetadata.StartFrom);
                jsonWriter.WriteValue(StartFrom.Value);				   
            }
            if (CacheControl.HasValue)
            {
                jsonWriter.WritePropertyName(SystemMetadata.CacheControl);
                jsonWriter.WriteValue((long) CacheControl.Value.TotalSeconds);
            }
            if (Acl != null)
            {
                jsonWriter.WritePropertyName(SystemMetadata.Acl);
                jsonWriter.WriteStartObject();
                if (Acl.ReadRole != null)
                {
                    jsonWriter.WritePropertyName(SystemMetadata.AclRead);
                    jsonWriter.WriteValue(Acl.ReadRole);
                }
                if (Acl.WriteRole != null)
                {
                    jsonWriter.WritePropertyName(SystemMetadata.AclWrite);
                    jsonWriter.WriteValue(Acl.WriteRole);
                }
                if (Acl.DeleteRole != null)
                {
                    jsonWriter.WritePropertyName(SystemMetadata.AclDelete);
                    jsonWriter.WriteValue(Acl.DeleteRole);
                }
                if (Acl.MetaReadRole != null)
                {
                    jsonWriter.WritePropertyName(SystemMetadata.AclMetaRead);
                    jsonWriter.WriteValue(Acl.MetaReadRole);
                }
                if (Acl.MetaWriteRole != null)
                {
                    jsonWriter.WritePropertyName(SystemMetadata.AclMetaWrite);
                    jsonWriter.WriteValue(Acl.MetaWriteRole);
                }
                jsonWriter.WriteEndObject();
            }
            jsonWriter.WriteEndObject();
        }
    }

    public class StreamAcl
    {
        public readonly string ReadRole;
        public readonly string WriteRole;
        public readonly string DeleteRole;
        public readonly string MetaReadRole;
        public readonly string MetaWriteRole;

        public StreamAcl(string readRole, string writeRole, string deleteRole, string metaReadRole, string metaWriteRole)
        {
            ReadRole = readRole;
            WriteRole = writeRole;
            DeleteRole = deleteRole;
            MetaReadRole = metaReadRole;
            MetaWriteRole = metaWriteRole;
        }

        public override string ToString()
        {
            return string.Format("Read: {0}, Write: {1}, Delete: {2}, MetaRead: {3}, MetaWrite: {4}",
                                 ReadRole, WriteRole, DeleteRole, MetaReadRole, MetaWriteRole);
        }
    }
}