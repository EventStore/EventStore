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
using EventStore.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Services.Processing
{

    public struct CheckpointTagVersion
    {
        public ProjectionVersion Version;
        public int SystemVersion;
        public CheckpointTag Tag;
        public Dictionary<string, JToken> ExtraMetadata;

        public CheckpointTag AdjustBy(PositionTagger tagger, ProjectionVersion version)
        {
            if (SystemVersion == Projections.VERSION && Version.Version == version.Version
                && Version.ProjectionId == version.ProjectionId)
                return Tag;

            return tagger.AdjustTag(Tag);
        }
    }

    public static class CheckpointTagExtensions
    {
        public static CheckpointTag ParseCheckpointTagJson(this string source)
        {
            if (string.IsNullOrEmpty(source))
                return null;
            var reader = new JsonTextReader(new StringReader(source));
            return CheckpointTag.FromJson(reader, default(ProjectionVersion)).Tag;
        }

        public static CheckpointTag ParseCheckpointTagJson(this byte[] source)
        {
            if (source == null || source.Length == 0)
                return null;
            var reader = new JsonTextReader(new StreamReader(new MemoryStream(source)));
            return CheckpointTag.FromJson(reader, default(ProjectionVersion)).Tag;
        }

        public static CheckpointTagVersion ParseCheckpointTagVersionExtraJson(this byte[] source, ProjectionVersion current)
        {
            if (source == null || source.Length == 0)
                return new CheckpointTagVersion { Version = new ProjectionVersion(current.ProjectionId, 0, 0), Tag = null };
            var reader = new JsonTextReader(new StreamReader(new MemoryStream(source)));
            return CheckpointTag.FromJson(reader, current);
        }

        public static CheckpointTagVersion ParseCheckpointTagVersionExtraJson(this string source, ProjectionVersion current)
        {
            if (string.IsNullOrEmpty(source))
                return new CheckpointTagVersion { Version = new ProjectionVersion(current.ProjectionId, 0, 0), Tag = null };
            var reader = new JsonTextReader(new StringReader(source));
            return CheckpointTag.FromJson(reader, current);
        }

        public static Dictionary<string, JToken> ParseCheckpointExtraJson(this string source)
        {
            if (string.IsNullOrEmpty(source))
                return null;
            var reader = new JsonTextReader(new StringReader(source));
            return CheckpointTag.FromJson(reader, default(ProjectionVersion)).ExtraMetadata;
        }

        public static string ParseCheckpointTagCorrelationId(this string source)
        {
            try
            {
                if (string.IsNullOrEmpty(source))
                    return null;
                var reader = new JsonTextReader(new StringReader(source));
                if (!reader.Read()) return null;
                if (reader.TokenType != JsonToken.StartObject) return null;
                while (true)
                {
                    CheckpointTag.Check(reader.Read(), reader);
                    if (reader.TokenType == JsonToken.EndObject)
                        break;
                    if (reader.TokenType != JsonToken.PropertyName) return null;
                    var name = (string) reader.Value;
                    switch (name)
                    {
                        default:
                            if (!reader.Read()) return null;
                            var jToken = JToken.ReadFrom(reader);
                            if (name == "$correlationId")
                                return jToken.ToString();
                            break;
                    }
                }
                return null;
            }
            catch (JsonReaderException ex)
            {
                return null;
            }
        }


    }
}
