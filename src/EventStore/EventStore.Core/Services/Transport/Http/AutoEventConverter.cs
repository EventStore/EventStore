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
using System.Xml.Linq;
using EventStore.Common.Log;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;
using System.Linq;

namespace EventStore.Core.Services.Transport.Http
{
    public static class AutoEventConverter
    {
        private static readonly ILogger Log = LogManager.GetLogger("AutoEventConverter");

        public static string SmartFormat(ClientMessage.ReadEventCompleted completed, ICodec targetCodec)
        {
            var dto = new HttpClientMessageDto.ReadEventCompletedText(completed);
            if (completed.Record.Flags.HasFlag(PrepareFlags.IsJson))
            {
                var deserializedData = Codec.Json.From<object>((string) dto.Data);
                var deserializedMetadata = Codec.Json.From<object>((string) dto.Metadata);

                if (deserializedData != null)
                    dto.Data = deserializedData;
                if (deserializedMetadata != null)
                    dto.Metadata = deserializedMetadata;
            }

            switch (targetCodec.ContentType)
            {
                case ContentType.Xml:
                case ContentType.ApplicationXml:
                case ContentType.Atom:
                {
                    var serializeObject = JsonConvert.SerializeObject(dto);
                    var deserializeXmlNode = JsonConvert.DeserializeXmlNode(serializeObject, "read-event-result");
                    return deserializeXmlNode.InnerXml;
                }

                default:
                    return targetCodec.To(dto);
            }
        }

        public static Tuple<int, Event[]> SmartParse(string request, ICodec sourceCodec)
        {
            var write = Load(request, sourceCodec);
            if (write == null || write.Events == null || write.Events.Length == 0)
                return new Tuple<int, Event[]>(-1, null);

            var events = Parse(write.Events);
            return new Tuple<int, Event[]>(write.ExpectedVersion, events);
        }

        private static HttpClientMessageDto.WriteEventsDynamic Load(string s, ICodec sourceCodec)
        {
            switch(sourceCodec.ContentType)
            {
                case ContentType.Json:
                case ContentType.AtomJson:
                    return LoadFromJson(s);

                case ContentType.Xml:
                case ContentType.ApplicationXml:
                case ContentType.Atom:
                    return LoadFromXml(s);

                default:
                    return null;
            }
        }

        private static HttpClientMessageDto.WriteEventsDynamic LoadFromJson(string json)
        {
            return Codec.Json.From<HttpClientMessageDto.WriteEventsDynamic>(json);
        }

        private static HttpClientMessageDto.WriteEventsDynamic LoadFromXml(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);

                XNamespace jsonNsValue = "http://james.newtonking.com/projects/json";
                XName jsonNsName = XNamespace.Xmlns + "json";

                doc.Root.SetAttributeValue(jsonNsName, jsonNsValue);

                var expectedVersion = doc.Root.Element("ExpectedVersion");
                var events = doc.Root.Descendants("event").ToArray();

                foreach (var @event in events)
                {
                    @event.Name = "Events";
                    @event.SetAttributeValue(jsonNsValue + "Array", "true");
                }

                doc.Root.ReplaceNodes(events);

                foreach (var element in doc.Root.Descendants("Data").Concat(doc.Root.Descendants("Metadata")))
                {
                    element.RemoveAttributes();
                }

                var json = JsonConvert.SerializeXNode(doc, Formatting.None, false);
                var root = JsonConvert.DeserializeObject<JObject>(json);
                var dynamicEvents = root["write-events"]["Events"].ToObject<HttpClientMessageDto.ClientEventDynamic[]>();
                return new HttpClientMessageDto.WriteEventsDynamic(int.Parse(expectedVersion.Value), dynamicEvents.ToArray());
            }
            catch (Exception e)
            {
                Log.InfoException(e, "Failed to load xml. Invalid format");
                return null;
            }
        }

        private static Event[] Parse(HttpClientMessageDto.ClientEventDynamic[] dynamicEvents)
        {
            var events = new Event[dynamicEvents.Length];
            for (int i = 0, n = dynamicEvents.Length; i < n; ++i)
            {
                var textEvent = dynamicEvents[i];
                bool dataIsJson;
                bool metadataIsJson;
                var data = AsBytes(textEvent.Data, out dataIsJson);
                var metadata = AsBytes(textEvent.Metadata, out metadataIsJson);

                events[i] = new Event(textEvent.EventId, textEvent.EventType, dataIsJson || metadataIsJson, data, metadata);
            }
            return events.ToArray();
        }

        private static byte[] AsBytes(object obj, out bool isJson)
        {
            if (obj is JObject)
            {
                isJson = true;
                return Encoding.UTF8.GetBytes(Codec.Json.To(obj));
            }

            isJson = false;
            return Encoding.UTF8.GetBytes((obj as string) ?? string.Empty);
        }
    }
}
