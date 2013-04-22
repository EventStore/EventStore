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
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Http.Codecs;
using NUnit.Framework;
using Newtonsoft.Json;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    internal static class FakeRequest
    {
        internal const string JsonData = "{\"field1\":\"value1\",\"field2\":\"value2\"}";
        internal const string JsonMetadata = "{\"meta-field1\":\"meta-value1\"}";
        internal const string JsonData2 = "{\"field1\":\"value1\",\"field2\":\"value2\"}";
        internal const string JsonMetadata2 = "{\"meta-field1\":\"meta-value1\"}";

        internal const string XmlData = "<field1>value1</field1><field2>value2</field2>";
        internal const string XmlMetadata = "<meta-field1>meta-value1</meta-field1>";
        internal const string XmlData2 = "<field1>value1</field1><field2>value2</field2>";
        internal const string XmlMetadata2 = "<meta-field1>meta-value1</meta-field1>";

        private static string JsonEventWriteFormat
        {
            get
            {
                return "{{\"eventId\":{0},\"eventType\":{1},\"data\":{2},\"metadata\":{3}}}";
            }
        }

        private static string JsonEventReadFormat
        {
            get
            {
                return "{{\"eventStreamId\":{0},\"eventNumber\":{1},\"eventType\":{2},\"data\":{3},\"metadata\":{4}}}";
            }
        }

        private static string XmlEventWriteFormat
        {
            get
            {
                return "<event><eventId>{0}</eventId><eventType>{1}</eventType><data>{2}</data><metadata>{3}</metadata></event>";
            }
        }

        private static string XmlEventReadFormat
        {
            get
            {
                return "<event><eventStreamId>{0}</eventStreamId><eventNumber>{1}</eventNumber><eventType>{2}</eventType><data>{3}</data><metadata>{4}</metadata></event>";
            }
        }

        public static string GetJsonWrite(string data, string metadata)
        {
            return GetJsonWrite(new[] {Tuple.Create(data, metadata)});
        }

        public static string GetJsonWrite(params Tuple<string, string>[] events)
        {
            return string.Format("[{0}]",
                string.Join(",", events.Select(x => 
                    string.Format(JsonEventWriteFormat, string.Format("\"{0}\"", Guid.NewGuid()), "\"type\"", x.Item1, x.Item2))));
        }

        public static string GetJsonEventReadResult(ResolvedEvent evnt, bool dataJson = true, bool metadataJson = true)
        {
            return string.Format(JsonEventReadFormat,
                                 WrapIntoQuotes(evnt.Event.EventStreamId),
                                 evnt.Event.EventNumber,
                                 WrapIntoQuotes(evnt.Event.EventType),
                                 dataJson ? JsonData : WrapIntoQuotes(AsString(evnt.Event.Data)),
                                 metadataJson ? JsonMetadata : WrapIntoQuotes(AsString(evnt.Event.Metadata)));
        }

        public static string GetJsonEventsReadResult(IEnumerable<ResolvedEvent> events, bool dataJson = true, bool metadataJson = true)
        {
            return string.Format("[{0}]", string.Join(",", events.Select(x => GetJsonEventReadResult(x, dataJson, metadataJson))));
        }

        public static string GetXmlWrite(string data, string metadata)
        {
            return GetXmlWrite(new[] {Tuple.Create(data, metadata)});
        }

        public static string GetXmlWrite(params Tuple<string, string>[] events)
        {
            return string.Format("<events>{0}</events>",
                string.Join("\n", events.Select(x =>
                    string.Format(XmlEventWriteFormat, Guid.NewGuid(), "type", x.Item1, x.Item2))));
        }

        public static string GetXmlEventReadResult(ResolvedEvent evnt, bool dataJson = true, bool metadataJson = true)
        {
            return string.Format(XmlEventReadFormat,                                  
                                 evnt.Event.EventStreamId,
                                 evnt.Event.EventNumber,
                                 evnt.Event.EventType,
                                 dataJson ? XmlData : AsString(evnt.Event.Data),
                                 metadataJson ? XmlMetadata : AsString(evnt.Event.Metadata));
        }

        public static string GetXmlEventsReadResult(IEnumerable<ResolvedEvent> events, bool dataJson = true, bool metadataJson = true)
        {
            return string.Format("<events>{0}</events>", 
                                 string.Join("\n", events.Select(x => GetXmlEventReadResult(x, dataJson, metadataJson))));
        }

        public static string AsString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes ?? new byte[0]);
        }

        private static string WrapIntoQuotes(string s)
        {
            return string.Format("\"{0}\"", s);
        }
    }

    internal abstract class do_not_use_indentation_for_json
    {
        [TestFixtureSetUp]
        public void SetUp()
        {
            JsonCodec.Formatting = Formatting.None;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            JsonCodec.Formatting = Formatting.Indented;
        }
    }

    [TestFixture]
    internal class when_writing_events_and_content_type_is_json : do_not_use_indentation_for_json
    {
        [Test]
        public void should_store_data_as_json_if_valid_and_metadata_as_string_if_not()
        {
            var codec = Codec.Json;
            var request = FakeRequest.GetJsonWrite(new[]
                                                   {
                                                           Tuple.Create(FakeRequest.JsonData, "\"metadata\""),
                                                           Tuple.Create(FakeRequest.JsonData2, "\"metadata2\"")
                                                   });

            var events = AutoEventConverter.SmartParse(request, codec);
            Assert.That(events.Length, Is.EqualTo(2));

            Assert.IsTrue(events[0].IsJson);
            Assert.That(ToString(events[0].Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(events[0].Metadata), Is.EqualTo("metadata"));

            Assert.IsTrue(events[1].IsJson);
            Assert.That(ToString(events[1].Data), Is.EqualTo(FakeRequest.JsonData2));
            Assert.That(ToString(events[1].Metadata), Is.EqualTo("metadata2"));
        }

        [Test]
        public void should_store_metadata_as_json_if_its_valid_and_data_as_string_if_its_not()
        {
            var codec = Codec.Json;
            var request = FakeRequest.GetJsonWrite(new[]
                                                   {
                                                        Tuple.Create("\"data\"", FakeRequest.JsonMetadata),
                                                        Tuple.Create("\"data2\"", FakeRequest.JsonMetadata2)
                                                   });

            var events = AutoEventConverter.SmartParse(request, codec);
            Assert.That(events.Length, Is.EqualTo(2));

            Assert.IsTrue(events[0].IsJson);
            Assert.That(ToString(events[0].Data), Is.EqualTo("data"));
            Assert.That(ToString(events[0].Metadata), Is.EqualTo(FakeRequest.JsonMetadata));

            Assert.IsTrue(events[1].IsJson);
            Assert.That(ToString(events[1].Data), Is.EqualTo("data2"));
            Assert.That(ToString(events[1].Metadata), Is.EqualTo(FakeRequest.JsonMetadata2));
        }

        [Test]
        public void should_store_both_data_and_metadata_as_json_if_both_are_valid_json_objects()
        {
            var codec = Codec.Json;
            var request = FakeRequest.GetJsonWrite(new[]
                                                   {
                                                        Tuple.Create(FakeRequest.JsonData, FakeRequest.JsonMetadata),
                                                        Tuple.Create(FakeRequest.JsonData2, FakeRequest.JsonMetadata2)
                                                   });

            var events = AutoEventConverter.SmartParse(request, codec);
            Assert.That(events.Length, Is.EqualTo(2));

            Assert.IsTrue(events[0].IsJson);
            Assert.That(ToString(events[0].Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(events[0].Metadata), Is.EqualTo(FakeRequest.JsonMetadata));

            Assert.IsTrue(events[1].IsJson);
            Assert.That(ToString(events[1].Data), Is.EqualTo(FakeRequest.JsonData2));
            Assert.That(ToString(events[1].Metadata), Is.EqualTo(FakeRequest.JsonMetadata2));
        }

        [Test]
        public void should_store_both_data_and_metadata_as_string_if_both_are_not_valid_json_objects()
        {
            var codec = Codec.Json;
            var request = FakeRequest.GetJsonWrite(new[]
                                                   {
                                                        Tuple.Create("\"data\"", "\"metadata\""),
                                                        Tuple.Create("\"data2\"", "\"metadata2\"")
                                                   });

            var events = AutoEventConverter.SmartParse(request, codec);
            Assert.That(events.Length, Is.EqualTo(2));

            Assert.IsFalse(events[0].IsJson);
            Assert.That(ToString(events[0].Data), Is.EqualTo("data"));
            Assert.That(ToString(events[0].Metadata), Is.EqualTo("metadata"));

            Assert.IsFalse(events[1].IsJson);
            Assert.That(ToString(events[1].Data), Is.EqualTo("data2"));
            Assert.That(ToString(events[1].Metadata), Is.EqualTo("metadata2"));
        }

        [Test]
        public void should_do_its_best_at_preserving_data_format_with_multiple_events()
        {
            var codec = Codec.Json;
            var request = FakeRequest.GetJsonWrite(new[]
                                                   {
                                                           Tuple.Create(FakeRequest.JsonData, FakeRequest.JsonMetadata),
                                                           Tuple.Create("\"data2\"", "\"metadata2\"")
                                                   });

            var events = AutoEventConverter.SmartParse(request, codec);
            Assert.That(events.Length, Is.EqualTo(2));

            Assert.IsTrue(events[0].IsJson);
            Assert.That(ToString(events[0].Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(events[0].Metadata), Is.EqualTo(FakeRequest.JsonMetadata));

            Assert.IsFalse(events[1].IsJson);
            Assert.That(ToString(events[1].Data), Is.EqualTo("data2"));
            Assert.That(ToString(events[1].Metadata), Is.EqualTo("metadata2"));
        }

        private string ToString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes ?? new byte[0]);
        }
    }

    [TestFixture]
    internal class when_writing_events_and_content_type_is_xml : do_not_use_indentation_for_json
    {
        [Test]
        public void should_convert_data_to_json_if_its_valid_xobject_and_metadata_as_string_if_its_not()
        {
            var codec = Codec.Xml;
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, "metadata");

            var events = AutoEventConverter.SmartParse(request, codec);
            var converted = events.Single();

            Assert.That(converted.IsJson);
            Assert.That(ToString(converted.Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(converted.Metadata), Is.EqualTo("metadata"));
        }

        [Test]
        public void should_convert_metadata_to_json_if_its_valid_xobject_and_data_as_string_if_its_not()
        {
            var codec = Codec.Xml;
            var request = FakeRequest.GetXmlWrite("data", FakeRequest.XmlMetadata);

            var events = AutoEventConverter.SmartParse(request, codec);
            var converted = events.Single();

            Assert.That(converted.IsJson);
            Assert.That(ToString(converted.Data), Is.EqualTo("data"));
            Assert.That(ToString(converted.Metadata), Is.EqualTo(FakeRequest.JsonMetadata));
        }

        [Test]
        public void should_convert_data_and_metadata_to_json_if_both_are_valid_xobjects()
        {
            var codec = Codec.Xml;
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, FakeRequest.XmlMetadata);

            var events = AutoEventConverter.SmartParse(request, codec);
            var converted = events.Single();

            Assert.That(converted.IsJson);
            Assert.That(ToString(converted.Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(converted.Metadata), Is.EqualTo(FakeRequest.JsonMetadata));
        }

        [Test]
        public void should_store_both_data_and_metadata_as_string_if_both_are_not_valid_xobjects_objects()
        {
            var codec = Codec.Xml;
            var request = FakeRequest.GetXmlWrite("data", "metadata");

            var events = AutoEventConverter.SmartParse(request, codec);
            var converted = events.Single();

            Assert.That(!converted.IsJson);
            Assert.That(ToString(converted.Data), Is.EqualTo("data"));
            Assert.That(ToString(converted.Metadata), Is.EqualTo("metadata"));
        }

        [Test]
        public void should_do_its_best_at_preserving_data_format_with_multiple_events()
        {
            var codec = Codec.Xml;
            var request = FakeRequest.GetXmlWrite(new[]
                                                  {
                                                        Tuple.Create(FakeRequest.XmlData, FakeRequest.XmlMetadata),
                                                        Tuple.Create("data2", "metadata2")
                                                  });

            var events = AutoEventConverter.SmartParse(request, codec);
            Assert.That(events.Length, Is.EqualTo(2));

            Assert.IsTrue(events[0].IsJson);
            Assert.That(ToString(events[0].Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(events[0].Metadata), Is.EqualTo(FakeRequest.JsonMetadata));

            Assert.IsFalse(events[1].IsJson);
            Assert.That(ToString(events[1].Data), Is.EqualTo("data2"));
            Assert.That(ToString(events[1].Metadata), Is.EqualTo("metadata2"));
        }


        private string ToString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes ?? new byte[0]);
        }
    }

    [TestFixture]
    internal class when_reading_events_and_accept_type_is_json : do_not_use_indentation_for_json
    {
        [Test]
        public void should_return_json_data_if_data_was_originally_written_as_xobject_and_metadata_as_string()
        {
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, "metadata");

            var events = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetJsonEventReadResult(resolvedEvent, dataJson: true, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_data_if_data_was_originally_written_as_jobject_and_metadata_as_string()
        {
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, "\"metadata\"");

            var events = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetJsonEventReadResult(resolvedEvent, dataJson: true, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_metadata_if_metadata_was_originally_written_as_xobject_and_data_as_string()
        {
            var request = FakeRequest.GetXmlWrite("data", FakeRequest.XmlMetadata);

            var events = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = events.Single();

            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetJsonEventReadResult(resolvedEvent, dataJson: false, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_metadata_if_metadata_was_originally_written_as_jobject_and_data_as_string()
        {
            var request = FakeRequest.GetJsonWrite("\"data\"", FakeRequest.JsonMetadata);

            var events = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetJsonEventReadResult(resolvedEvent, dataJson: false, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_data_and_json_metadata_if_both_were_written_as_xobjects()
        {
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, FakeRequest.XmlMetadata);

            var events = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetJsonEventReadResult(resolvedEvent, dataJson: true, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_data_and_json_metadata_if_both_were_written_as_jobjects()
        {
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, FakeRequest.JsonMetadata);

            var events = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetJsonEventReadResult(resolvedEvent, dataJson: true, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_string_data_and_string_metadata_if_both_were_written_as_string_using_json_write()
        {
            var request = FakeRequest.GetJsonWrite("\"data\"", "\"metadata\"");

            var events = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetJsonEventReadResult(resolvedEvent, dataJson: false, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_string_data_and_string_metadata_if_both_were_written_as_string_using_xml_write()
        {
            var request = FakeRequest.GetXmlWrite("data", "metadata");

            var events = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetJsonEventReadResult(resolvedEvent, dataJson: false, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        private ResolvedEvent GenerateResolvedEvent(byte[] data, byte[] metadata)
        {
            return new ResolvedEvent(new EventRecord(0, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, 
                                     DateTime.MinValue, PrepareFlags.IsJson, "type", data, metadata));
        }
    }

    [TestFixture]
    internal class when_reading_events_and_accept_type_is_xml : do_not_use_indentation_for_json
    {
        [Test]
        public void should_return_xml_data_if_data_was_originally_written_as_xobject_and_metadata_as_string()
        {
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, "metadata");

            var events = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetXmlEventReadResult(resolvedEvent, dataJson: true, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_data_if_data_was_originally_written_as_jobject_and_metadata_as_string()
        {
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, "\"metadata\"");

            var events = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetXmlEventReadResult(resolvedEvent, dataJson: true, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_metadata_if_metadata_was_originally_written_as_xobject_and_data_as_string()
        {
            var request = FakeRequest.GetXmlWrite("data", FakeRequest.XmlMetadata);

            var events = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetXmlEventReadResult(resolvedEvent, dataJson: false, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_metadata_if_metadata_was_originally_written_as_jobject_and_data_as_string()
        {
            var request = FakeRequest.GetJsonWrite("\"data\"", FakeRequest.JsonMetadata);

            var events = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetXmlEventReadResult(resolvedEvent, dataJson: false, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_data_and_xml_metadata_if_both_were_written_as_xobjects()
        {
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, FakeRequest.XmlMetadata);

            var events = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetXmlEventReadResult(resolvedEvent, dataJson: true, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_data_and_xml_metadata_if_both_were_written_as_jobjects()
        {
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, FakeRequest.JsonMetadata);

            var events = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetXmlEventReadResult(resolvedEvent, dataJson: true, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_string_data_and_string_metadata_if_both_were_written_as_string_using_json_write()
        {
            var request = FakeRequest.GetJsonWrite("\"data\"", "\"metadata\"");

            var events = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetXmlEventReadResult(resolvedEvent, dataJson: false, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_string_data_and_string_metadata_if_both_were_written_as_string_using_xml_write()
        {
            var request = FakeRequest.GetXmlWrite("data", "metadata");

            var events = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = events.Single();
            var resolvedEvent = GenerateResolvedEvent(evnt.Data, evnt.Metadata);
            var expected = FakeRequest.GetXmlEventReadResult(resolvedEvent, dataJson: false, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(resolvedEvent, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        private ResolvedEvent GenerateResolvedEvent(byte[] data, byte[] metadata)
        {
            return new ResolvedEvent(new EventRecord(0, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0,
                                     DateTime.MinValue, PrepareFlags.IsJson, "type", data, metadata));
        }
    }
}
