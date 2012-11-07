using System;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using System.Linq;
using Newtonsoft.Json;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    internal static class FakeRequest
    {
        internal const string JsonData = "{\"field1\":\"value1\",\"field2\":\"value2\"}";
        internal const string JsonMetadata = "{\"meta-field1\":\"meta-value1\"}";

        internal const string XmlData = "<field1>value1</field1><field2>value2</field2>";
        internal const string XmlMetadata = "<meta-field1>meta-value1</meta-field1>";

        private static string JsonWriteFormat
        {
            get
            {
                return "{{\"expectedVersion\":{0},\"events\":[{{\"eventId\":{1},\"eventType\":{2},\"data\":{3},\"metadata\":{4}}}]}}";
            }
        }

        private static string JsonReadFormat
        {
            get
            {
                return "{{\"eventStreamId\":{0},\"eventNumber\":{1},\"eventType\":{2},\"data\":{3},\"metadata\":{4}}}";
            }
        }

        private static string XmlWriteFormat
        {
            get
            {
                return "<write-events><ExpectedVersion>{0}</ExpectedVersion><Events><event><EventId>{1}</EventId><EventType>{2}</EventType><Data>{3}</Data><Metadata>{4}</Metadata></event></Events></write-events>";
            }
        }

        private static string XmlReadFormat
        {
            get
            {
                return "<read-event-result><EventStreamId>{0}</EventStreamId><EventNumber>{1}</EventNumber><EventType>{2}</EventType><Data>{3}</Data><Metadata>{4}</Metadata></read-event-result>";
            }
        }

        public static string GetJsonWrite(string data, string metadata)
        {
            return string.Format(JsonWriteFormat,
                                 "-1",
                                 String.Format("\"{0}\"", Guid.NewGuid()),
                                 "\"type\"",
                                 data,
                                 metadata);
        }

        public static string GetJsonReadResult(ClientMessage.ReadEventCompleted completed, bool dataJson = true, bool metadataJson = true)
        {
            return string.Format(JsonReadFormat,
                                 WrapIntoQuotes(completed.Record.EventStreamId),
                                 completed.Record.EventNumber,
                                 WrapIntoQuotes(completed.Record.EventType),
                                 dataJson ? JsonData : WrapIntoQuotes(AsString(completed.Record.Data)),
                                 metadataJson ? JsonMetadata : WrapIntoQuotes(AsString(completed.Record.Metadata)));
        }

        public static string GetXmlWrite(string data, string metadata)
        {
            return string.Format(XmlWriteFormat, "-1", Guid.NewGuid(), "type", data, metadata);
        }

        public static string GetXmlReadResult(ClientMessage.ReadEventCompleted completed, bool dataJson = true, bool metadataJson = true)
        {
            return string.Format(XmlReadFormat,
                                 completed.Record.EventStreamId,
                                 completed.Record.EventNumber,
                                 completed.Record.EventType,
                                 dataJson ? XmlData : AsString(completed.Record.Data),
                                 metadataJson ? XmlMetadata : AsString(completed.Record.Metadata));
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

    internal class do_not_use_indentation_for_json
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
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, "\"metadata\"");

            var tuple = AutoEventConverter.SmartParse(request, codec);
            var evnt = tuple.Item2.Single();

            Assert.That(evnt.IsJson);
            Assert.That(ToString(evnt.Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(evnt.Metadata), Is.EqualTo("metadata"));
        }

        [Test]
        public void should_store_metadata_as_json_if_its_valid_and_data_as_string_if_its_not()
        {
            var codec = Codec.Json;
            var request = FakeRequest.GetJsonWrite("\"data\"", FakeRequest.JsonMetadata);

            var tuple = AutoEventConverter.SmartParse(request, codec);
            var evnt = tuple.Item2.Single();

            Assert.That(evnt.IsJson);
            Assert.That(ToString(evnt.Data), Is.EqualTo("data"));
            Assert.That(ToString(evnt.Metadata), Is.EqualTo(FakeRequest.JsonMetadata));
        }

        [Test]
        public void should_store_both_data_and_metadata_as_json_if_both_are_valid_json_objects()
        {
            var codec = Codec.Json;
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, FakeRequest.JsonMetadata);

            var tuple = AutoEventConverter.SmartParse(request, codec);
            var evnt = tuple.Item2.Single();

            Assert.That(evnt.IsJson);
            Assert.That(ToString(evnt.Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(evnt.Metadata), Is.EqualTo(FakeRequest.JsonMetadata));
        }

        [Test]
        public void should_store_both_data_and_metadata_as_string_if_both_are_not_valid_json_objects()
        {
            var codec = Codec.Json;
            var request = FakeRequest.GetJsonWrite("\"data\"", "\"metadata\"");

            var tuple = AutoEventConverter.SmartParse(request, codec);
            var evnt = tuple.Item2.Single();

            Assert.That(!evnt.IsJson);
            Assert.That(ToString(evnt.Data), Is.EqualTo("data"));
            Assert.That(ToString(evnt.Metadata), Is.EqualTo("metadata"));
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

            var tuple = AutoEventConverter.SmartParse(request, codec);
            var converted = tuple.Item2.Single();

            Assert.That(converted.IsJson);
            Assert.That(ToString(converted.Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(converted.Metadata), Is.EqualTo("metadata"));
        }

        [Test]
        public void should_convert_metadata_to_json_if_its_valid_xobject_and_data_as_string_if_its_not()
        {
            var codec = Codec.Xml;
            var request = FakeRequest.GetXmlWrite("data", FakeRequest.XmlMetadata);

            var tuple = AutoEventConverter.SmartParse(request, codec);
            var converted = tuple.Item2.Single();

            Assert.That(converted.IsJson);
            Assert.That(ToString(converted.Data), Is.EqualTo("data"));
            Assert.That(ToString(converted.Metadata), Is.EqualTo(FakeRequest.JsonMetadata));
        }

        [Test]
        public void should_convert_data_and_metadata_to_json_if_both_are_valid_xobjects()
        {
            var codec = Codec.Xml;
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, FakeRequest.XmlMetadata);

            var tuple = AutoEventConverter.SmartParse(request, codec);
            var converted = tuple.Item2.Single();

            Assert.That(converted.IsJson);
            Assert.That(ToString(converted.Data), Is.EqualTo(FakeRequest.JsonData));
            Assert.That(ToString(converted.Metadata), Is.EqualTo(FakeRequest.JsonMetadata));
        }

        [Test]
        public void should_store_both_data_and_metadata_as_string_if_both_are_not_valid_xobjects_objects()
        {
            var codec = Codec.Xml;
            var request = FakeRequest.GetXmlWrite("data", "metadata");

            var tuple = AutoEventConverter.SmartParse(request, codec);
            var converted = tuple.Item2.Single();

            Assert.That(!converted.IsJson);
            Assert.That(ToString(converted.Data), Is.EqualTo("data"));
            Assert.That(ToString(converted.Metadata), Is.EqualTo("metadata"));
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

            var written = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetJsonReadResult(readCompleted, dataJson: true, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_data_if_data_was_originally_written_as_jobject_and_metadata_as_string()
        {
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, "\"metadata\"");

            var written = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetJsonReadResult(readCompleted, dataJson: true, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_metadata_if_metadata_was_originally_written_as_xobject_and_data_as_string()
        {
            var request = FakeRequest.GetXmlWrite("data", FakeRequest.XmlMetadata);

            var written = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetJsonReadResult(readCompleted, dataJson: false, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_metadata_if_metadata_was_originally_written_as_jobject_and_data_as_string()
        {
            var request = FakeRequest.GetJsonWrite("\"data\"", FakeRequest.JsonMetadata);

            var written = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetJsonReadResult(readCompleted, dataJson: false, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_data_and_json_metadata_if_both_were_written_as_xobjects()
        {
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, FakeRequest.XmlMetadata);

            var written = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetJsonReadResult(readCompleted, dataJson: true, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_json_data_and_json_metadata_if_both_were_written_as_jobjects()
        {
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, FakeRequest.JsonMetadata);

            var written = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetJsonReadResult(readCompleted, dataJson: true, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_string_data_and_string_metadata_if_both_were_written_as_string_using_json_write()
        {
            var request = FakeRequest.GetJsonWrite("\"data\"", "\"metadata\"");

            var written = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetJsonReadResult(readCompleted, dataJson: false, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_string_data_and_string_metadata_if_both_were_written_as_string_using_xml_write()
        {
            var request = FakeRequest.GetXmlWrite("data", "metadata");

            var written = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetJsonReadResult(readCompleted, dataJson: false, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Json);

            Assert.That(converted, Is.EqualTo(expected));
        }

        private ClientMessage.ReadEventCompleted GenereteReadCompleted(byte[] data, byte[] metadata)
        {
            return new ClientMessage.ReadEventCompleted(Guid.Empty,
                                                        "stream",
                                                        0,
                                                        SingleReadResult.Success,
                                                        new EventRecord(
                                                            0,
                                                            0,
                                                            Guid.NewGuid(),
                                                            Guid.NewGuid(),
                                                            0,
                                                            0,
                                                            "stream",
                                                            0,
                                                            DateTime.MinValue,
                                                            PrepareFlags.IsJson,
                                                            "type",
                                                            data,
                                                            metadata));
        }
    }

    [TestFixture]
    internal class when_reading_events_and_accept_type_is_xml : do_not_use_indentation_for_json
    {
        [Test]
        public void should_return_xml_data_if_data_was_originally_written_as_xobject_and_metadata_as_string()
        {
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, "metadata");

            var written = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetXmlReadResult(readCompleted, dataJson: true, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_data_if_data_was_originally_written_as_jobject_and_metadata_as_string()
        {
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, "\"metadata\"");

            var written = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetXmlReadResult(readCompleted, dataJson: true, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_metadata_if_metadata_was_originally_written_as_xobject_and_data_as_string()
        {
            var request = FakeRequest.GetXmlWrite("data", FakeRequest.XmlMetadata);

            var written = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetXmlReadResult(readCompleted, dataJson: false, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_metadata_if_metadata_was_originally_written_as_jobject_and_data_as_string()
        {
            var request = FakeRequest.GetJsonWrite("\"data\"", FakeRequest.JsonMetadata);

            var written = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetXmlReadResult(readCompleted, dataJson: false, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_data_and_xml_metadata_if_both_were_written_as_xobjects()
        {
            var request = FakeRequest.GetXmlWrite(FakeRequest.XmlData, FakeRequest.XmlMetadata);

            var written = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetXmlReadResult(readCompleted, dataJson: true, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_xml_data_and_xml_metadata_if_both_were_written_as_jobjects()
        {
            var request = FakeRequest.GetJsonWrite(FakeRequest.JsonData, FakeRequest.JsonMetadata);

            var written = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetXmlReadResult(readCompleted, dataJson: true, metadataJson: true);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_string_data_and_string_metadata_if_both_were_written_as_string_using_json_write()
        {
            var request = FakeRequest.GetJsonWrite("\"data\"", "\"metadata\"");

            var written = AutoEventConverter.SmartParse(request, Codec.Json);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetXmlReadResult(readCompleted, dataJson: false, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        [Test]
        public void should_return_string_data_and_string_metadata_if_both_were_written_as_string_using_xml_write()
        {
            var request = FakeRequest.GetXmlWrite("data", "metadata");

            var written = AutoEventConverter.SmartParse(request, Codec.Xml);
            var evnt = written.Item2.Single();

            var readCompleted = GenereteReadCompleted(evnt.Data, evnt.Metadata);

            var expected = FakeRequest.GetXmlReadResult(readCompleted, dataJson: false, metadataJson: false);
            var converted = AutoEventConverter.SmartFormat(readCompleted, Codec.Xml);

            Assert.That(converted, Is.EqualTo(expected));
        }

        private ClientMessage.ReadEventCompleted GenereteReadCompleted(byte[] data, byte[] metadata)
        {
            return new ClientMessage.ReadEventCompleted(Guid.Empty,
                                                        "stream",
                                                        0,
                                                        SingleReadResult.Success,
                                                        new EventRecord(
                                                            0,
                                                            0,
                                                            Guid.NewGuid(),
                                                            Guid.NewGuid(),
                                                            0,
                                                            0,
                                                            "stream",
                                                            0,
                                                            DateTime.MinValue,
                                                            PrepareFlags.IsJson,
                                                            "type",
                                                            data,
                                                            metadata));
        }
    }
}
