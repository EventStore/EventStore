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
using EventStore.Transport.Http.Codecs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;
using System.Linq;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.Transport.Http
{
    public static class AutoEventConverter
    {
        private static readonly ILogger Log = LogManager.GetLogger("AutoEventConverter");

        public static string SmartFormat(ResolvedEvent evnt, ICodec targetCodec)
        {
            var dto = new HttpClientMessageDto.ReadEventCompletedText(evnt);
            if (evnt.Event.Flags.HasFlag(PrepareFlags.IsJson))
            {
                var deserializedData = Codec.Json.From<object>((string) dto.data);
                var deserializedMetadata = Codec.Json.From<object>((string) dto.metadata);

                if (deserializedData != null)
                    dto.data = deserializedData;
                if (deserializedMetadata != null)
                    dto.metadata = deserializedMetadata;
            }

            switch (targetCodec.ContentType)
            {
                case ContentType.Xml:
                case ContentType.ApplicationXml:
                    {
                        var serializeObject = JsonConvert.SerializeObject(dto.data);
                        var deserializeXmlNode = JsonConvert.DeserializeXmlNode(serializeObject, "data");
                        return deserializeXmlNode.InnerXml;
                    }
                case ContentType.Json:
                    return targetCodec.To(dto.data);


                case ContentType.Atom:
                case ContentType.EventXml:
                {
                    var serializeObject = JsonConvert.SerializeObject(dto);
                    var deserializeXmlNode = JsonConvert.DeserializeXmlNode(serializeObject, "event");
                    return deserializeXmlNode.InnerXml;
                }

                case ContentType.EventJson:
                    return targetCodec.To(dto);


                default:
                    throw new NotSupportedException();
            }
        }

        public static Event[] SmartParse(string request, ICodec sourceCodec)
        {
            var writeEvents = Load(request, sourceCodec);
            if (writeEvents.IsEmpty())
                return null;
            var events = Parse(writeEvents);
            return events;
        }

        private static HttpClientMessageDto.ClientEventDynamic[] Load(string data, ICodec sourceCodec)
        {
            switch(sourceCodec.ContentType)
            {
                case ContentType.Json:
                case ContentType.EventsJson:
                case ContentType.AtomJson:
                    return LoadFromJson(data);

                case ContentType.Xml:
                case ContentType.EventsXml:
                case ContentType.ApplicationXml:
                case ContentType.Atom:
                    return LoadFromXml(data);

                default:
                    return null;
            }
        }

        private static HttpClientMessageDto.ClientEventDynamic[] LoadFromJson(string json)
        {
            return Codec.Json.From<HttpClientMessageDto.ClientEventDynamic[]>(json);
        }

        private static HttpClientMessageDto.ClientEventDynamic[] LoadFromXml(string xml)
        {
            try
            {
                XDocument doc = XDocument.Parse(xml);

                XNamespace jsonNsValue = "http://james.newtonking.com/projects/json";
                XName jsonNsName = XNamespace.Xmlns + "json";

                doc.Root.SetAttributeValue(jsonNsName, jsonNsValue);

                var events = doc.Root.Elements()/*.ToArray()*/;
                foreach (var @event in events)
                {
                    @event.Name = "events";
                    @event.SetAttributeValue(jsonNsValue + "Array", "true");
                }
                //doc.Root.ReplaceNodes(events);
//                foreach (var element in doc.Root.Descendants("data").Concat(doc.Root.Descendants("metadata")))
//                {
//                    element.RemoveAttributes();
//                }

                var json = JsonConvert.SerializeXNode(doc.Root, Formatting.None, true);
                var root = JsonConvert.DeserializeObject<HttpClientMessageDto.WriteEventsDynamic>(json);
                return root.events;
//                var root = JsonConvert.DeserializeObject<JObject>(json);
//                var dynamicEvents = root.ToObject<HttpClientMessageDto.WriteEventsDynamic>();
//                return dynamicEvents.events;
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
                var data = AsBytes(textEvent.data, out dataIsJson);
                var metadata = AsBytes(textEvent.metadata, out metadataIsJson);

                events[i] = new Event(textEvent.eventId, textEvent.eventType, dataIsJson || metadataIsJson, data, metadata);
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
