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
using System.Xml;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Services.Transport.Http
{
    public static class EventConvertion
    {
        public static Event ConvertOnWrite(ClientMessageDto.EventText eventText)
        {
            object data;
            object metadata;

            if (TryLoadAsObject(eventText.Data, out data) && TryLoadAsObject(eventText.Metadata, out metadata))
            {
                return new Event(eventText.EventId,
                                 eventText.EventType,
                                 true,
                                 Encoding.UTF8.GetBytes(Codec.Json.To(data)),
                                 Encoding.UTF8.GetBytes(Codec.Json.To(metadata)));
            }
            return new Event(eventText.EventId,
                             eventText.EventType,
                             false,
                             Encoding.UTF8.GetBytes(ToString(eventText.Data)),
                             Encoding.UTF8.GetBytes(ToString(eventText.Metadata)));
        }

        public static string ConvertOnRead(ClientMessage.ReadEventCompleted completed, ICodec responseCodec)
        {
            var dto = new ClientMessageDto.ReadEventCompletedText(completed);

            if (completed.Record.Flags.HasFlag(PrepareFlags.IsJson))
            {
                dto.Data = Codec.Json.From<object>((string) dto.Data);
                dto.Metadata = Codec.Json.From<object>((string) dto.Metadata);
            }

            var type = responseCodec.GetType();
            type = type == typeof (CustomCodec) ? ((CustomCodec) responseCodec).BaseCodec.GetType() : type;
            return type == typeof(XmlCodec) ? Codec.Json.ToXmlUsingJson(dto) : responseCodec.To(dto);
        }

        private static bool TryLoadAsObject(object value, out object result)
        {
            if (!(value is string))
            {
                result = value;
                return true;
            }

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml((string)value);

                result = JsonConvert.DeserializeObject(JsonConvert.SerializeXmlNode(doc));
                return true;
            }

            catch
            {
                result = null;
                return false;
            }
        }

        private static string ToString(object obj)
        {
            return (obj is JObject ? Codec.Json.To(obj) : (string) obj) ?? String.Empty;
        }
    }
}
