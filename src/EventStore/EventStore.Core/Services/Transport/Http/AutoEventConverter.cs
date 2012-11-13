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

        public static Tuple<int, Event[]> SmartParse(string request, ICodec sourceCodec)
        {
            var write = Load(request, sourceCodec);
            if (write == null || write.Events == null || write.Events.Length == 0)
                return new Tuple<int, Event[]>(-1, null);

            var events = Parse(write.Events);
            return new Tuple<int, Event[]>(write.ExpectedVersion, events);
        }
 
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

            if (new[] { ContentType.Xml, "application/xml", ContentType.Atom }.Contains(targetCodec.ContentType))
            {
                var serializeObject = JsonConvert.SerializeObject(dto);
                var deserializeXmlNode = JsonConvert.DeserializeXmlNode(serializeObject, "read-event-result");
                return deserializeXmlNode.InnerXml;
            }

            return targetCodec.To(dto);
        }

        private static HttpClientMessageDto.WriteEventsDynamic Load(string s, ICodec sourceCodec)
        {
            var requestType = sourceCodec.ContentType;

            if (new[] {ContentType.Json, ContentType.AtomJson}.Contains(requestType))
                return LoadFromJson(s);
            if (new[] {ContentType.Xml, "application/xml", ContentType.Atom}.Contains(requestType))
                return LoadFromXml(s);

            return null;
        }

        private static HttpClientMessageDto.WriteEventsDynamic LoadFromJson(string json)
        {
            return Codec.Json.From<HttpClientMessageDto.WriteEventsDynamic>(json);
        }

        private static HttpClientMessageDto.WriteEventsDynamic LoadFromXml(string xml)
        {
            try
            {
                XDocument doc;
                using(var reader = new StringReader(xml))
                    doc = XDocument.Load(reader);

                XNamespace jsonNs = "http://james.newtonking.com/projects/json";
                XName jsonName = XNamespace.Xmlns + "json";

                doc.Root.SetAttributeValue(jsonName, jsonNs);

                var expectedVersion = doc.Root.Element("ExpectedVersion");
                var events = doc.Root.Descendants("event").ToArray();

                foreach (var @event in events)
                {
                    @event.Name = "Events";
                    @event.SetAttributeValue(jsonNs + "Array", "true");
                }

                doc.Root.ReplaceNodes(events);

                foreach (var element in doc.Root.Descendants("Data").Concat(doc.Root.Descendants("Metadata")))
                    element.RemoveAttributes();

                var json = JsonConvert.SerializeXNode(doc, Formatting.None, false);
                var dynamicEvents = JsonConvert.DeserializeObject<JObject>(json)["write-events"]["Events"].ToObject<HttpClientMessageDto.ClientEventDynamic[]>();
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
            var events = new List<Event>(dynamicEvents.Length);
            foreach (var textEvent in dynamicEvents)
            {
                bool dataIsJson;
                bool metadataIsJson;
                var data = AsBytes(textEvent.Data, out dataIsJson);
                var metadata = AsBytes(textEvent.Metadata, out metadataIsJson);

                events.Add(new Event(textEvent.EventId, textEvent.EventType, dataIsJson || metadataIsJson, data, metadata));
            }
            return events.ToArray();
        }

        private static byte[] AsBytes(object obj, out bool isJson)
        {
            isJson = true;
            if (IsJObject(obj))
                return Encoding.UTF8.GetBytes(Codec.Json.To(obj));

            isJson = false;
            return Encoding.UTF8.GetBytes(AsString(obj));
        }

        private static string AsString(object obj)
        {
            if(obj == null)
                return string.Empty;
            return (obj as string) ?? string.Empty;
        }

        private static bool IsJObject(object obj)
        {
            return obj is JObject;
        }
    }
}
