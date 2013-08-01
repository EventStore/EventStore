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
    public class SystemSettings
    {
        public static readonly SystemSettings Default = new SystemSettings(
                new StreamAcl(SystemRoles.All, SystemRoles.All, SystemRoles.All, SystemRoles.All, SystemRoles.All),
                new StreamAcl(SystemRoles.Admins, SystemRoles.Admins, SystemRoles.Admins, SystemRoles.Admins, SystemRoles.Admins));

        public readonly StreamAcl UserStreamAcl;
        public readonly StreamAcl SystemStreamAcl;

        public SystemSettings(StreamAcl userStreamAcl, StreamAcl systemStreamAcl)
        {
            UserStreamAcl = userStreamAcl;
            SystemStreamAcl = systemStreamAcl;
        }

        public override string ToString()
        {
            return string.Format("UserStreamAcl: ({0}), SystemStreamAcl: ({1})", UserStreamAcl, SystemStreamAcl);
        }

        public static SystemSettings FromJsonBytes(byte[] json)
        {
            using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(json))))
            {
                Check(reader.Read(), reader);
                Check(JsonToken.StartObject, reader);

                StreamAcl userStreamAcl = null;
                StreamAcl systemStreamAcl = null;

                while (true)
                {
                    Check(reader.Read(), reader);
                    if (reader.TokenType == JsonToken.EndObject)
                        break;
                    Check(JsonToken.PropertyName, reader);
                    var name = (string)reader.Value;
                    switch (name)
                    {
                        case SystemMetadata.UserStreamAcl: userStreamAcl = StreamMetadata.ReadAcl(reader); break;
                        case SystemMetadata.SystemStreamAcl: systemStreamAcl = StreamMetadata.ReadAcl(reader); break;
                        default:
                        {
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
            if (UserStreamAcl != null)
            {
                jsonWriter.WritePropertyName(SystemMetadata.UserStreamAcl);
                StreamMetadata.WriteAcl(jsonWriter, UserStreamAcl);
            }
            if (SystemStreamAcl != null)
            {
                jsonWriter.WritePropertyName(SystemMetadata.SystemStreamAcl);
                StreamMetadata.WriteAcl(jsonWriter, SystemStreamAcl);
            }
            jsonWriter.WriteEndObject();
        }
    }
}