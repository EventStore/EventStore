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
using System.Text;
using EventStore.Common.Log;
using EventStore.Transport.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace EventStore.Core.Services.Transport.Http.Codecs
{
    public class JsonCodec : ICodec
    {
        public static Formatting Formatting = Formatting.Indented;

        private static readonly ILogger Log = LogManager.GetLoggerFor<JsonCodec>();

        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            Converters = new JsonConverter[]
            {
                new StringEnumConverter()
            }
        };


        public string ContentType { get { return EventStore.Transport.Http.ContentType.Json; } }
        public Encoding Encoding { get { return Encoding.UTF8; } }

        public bool CanParse(string format)
        {
            return string.Equals(ContentType, format, StringComparison.OrdinalIgnoreCase);
        }

        public bool SuitableForReponse(AcceptComponent component)
        {
            return component.MediaType == "*"
                   || (string.Equals(component.MediaType, "application", StringComparison.OrdinalIgnoreCase)
                       && (component.MediaSubtype == "*"
                           || string.Equals(component.MediaSubtype, "json", StringComparison.OrdinalIgnoreCase)));
        }

        public T From<T>(string text)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(text, JsonSettings);
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "'{0}' is not a valid serialized {1}", text, typeof(T).FullName);
                return default(T);
            }
        }

        public string To<T>(T value)
        {
            try
            {
                return JsonConvert.SerializeObject(value, Formatting, JsonSettings);
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error serializing object {0}", value);
                return null;
            }
        }
    }
}