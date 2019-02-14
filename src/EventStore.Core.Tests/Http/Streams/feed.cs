using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Transport.Http;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System.Linq;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Http.Streams {
	namespace feed {
		public abstract class SpecificationWithLongFeed : HttpBehaviorSpecification {
			protected int _numberOfEvents;

			protected override void Given() {
				_numberOfEvents = 25;
				for (var i = 0; i < _numberOfEvents; i++) {
					PostEvent(i);
				}
			}

			protected string PostEvent(int i) {
				var response = MakeArrayEventsPost(
					TestStream,
					new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {Number = i}}});
				Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
				return response.Headers[HttpResponseHeader.Location];
			}

			protected string GetLink(JObject feed, string relation) {
				var rel = (from JObject link in feed["links"]
					from JProperty attr in link
					where attr.Name == "relation" && (string)attr.Value == relation
					select link).SingleOrDefault();
				return (rel == null) ? (string)null : (string)rel["uri"];
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_posting_multiple_events : SpecificationWithLongFeed {
			protected override void When() {
				GetJson<JObject>(TestStream, ContentType.AtomJson);
			}

			[Test]
			public void returns_ok_status_code() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_feed_head : SpecificationWithLongFeed {
			private JObject _feed;

			protected override void When() {
				_feed = GetJson<JObject>(TestStream, ContentType.AtomJson);
			}

			[Test]
			public void returns_ok_status_code() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void contains_a_link_rel_previous() {
				var rel = GetLink(_feed, "previous");
				Assert.IsNotEmpty(rel);
			}

			[Test]
			public void contains_a_link_rel_next() {
				var rel = GetLink(_feed, "next");
				Assert.IsNotEmpty(rel);
			}

			[Test]
			public void contains_a_link_rel_self() {
				var rel = GetLink(_feed, "self");
				Assert.IsNotEmpty(rel);
			}

			[Test]
			public void contains_a_link_rel_first() {
				var rel = GetLink(_feed, "first");
				Assert.IsNotEmpty(rel);
			}

			[Test]
			public void contains_a_link_rel_last() {
				var rel = GetLink(_feed, "last");
				Assert.IsNotEmpty(rel);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_feed_head_with_forwarded_prefix : SpecificationWithLongFeed {
			private JObject _feed;
			private string _prefix;

			protected override void When() {
				_prefix = "testprefix";
				var headers = new NameValueCollection();
				headers.Add("X-Forwarded-Prefix", _prefix);
				_feed = GetJson<JObject>(TestStream, ContentType.AtomJson, headers: headers);
			}

			[Test]
			public void returns_ok_status_code() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void contains_a_link_rel_previous_with_prefix() {
				var rel = GetLink(_feed, "previous");
				Assert.AreEqual(MakeUrl("/" + _prefix + TestStream + "/25/forward/20"), new Uri(rel));
			}

			[Test]
			public void contains_a_link_rel_next_with_prefix() {
				var rel = GetLink(_feed, "next");
				Assert.AreEqual(MakeUrl("/" + _prefix + TestStream + "/4/backward/20"), new Uri(rel));
			}

			[Test]
			public void contains_a_link_rel_self_with_prefix() {
				var rel = GetLink(_feed, "self");
				Assert.AreEqual(MakeUrl("/" + _prefix + TestStream), new Uri(rel));
			}

			[Test]
			public void contains_a_link_rel_first_with_prefix() {
				var rel = GetLink(_feed, "first");
				Assert.AreEqual(MakeUrl("/" + _prefix + TestStream + "/head/backward/20"), new Uri(rel));
			}

			[Test]
			public void contains_a_link_rel_last_with_prefix() {
				var rel = GetLink(_feed, "last");
				Assert.AreEqual(MakeUrl("/" + _prefix + TestStream + "/0/forward/20"), new Uri(rel));
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_the_previous_link_of_the_feed_head : SpecificationWithLongFeed {
			private JObject _feed;
			private JObject _head;
			private string _previous;

			protected override void Given() {
				base.Given();
				_head = GetJson<JObject>(TestStream, ContentType.AtomJson);
				_previous = GetLink(_head, "previous");
			}

			protected override void When() {
				_feed = GetJson<JObject>(_previous, ContentType.AtomJson);
			}

			[Test]
			public void returns_200_response() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void there_is_no_prev_link() {
				var rel = GetLink(_feed, "prev");
				Assert.IsNull(rel);
			}

			[Test]
			public void there_is_a_next_link() {
				var rel = GetLink(_feed, "next");
				Assert.IsNotEmpty(rel);
			}

			[Test]
			public void there_is_a_self_link() {
				var rel = GetLink(_feed, "self");
				Assert.IsNotEmpty(rel);
			}

			[Test]
			public void there_is_a_first_link() {
				var rel = GetLink(_feed, "first");
				Assert.IsNotEmpty(rel);
			}

			[Test]
			public void there_is_a_last_link() {
				var rel = GetLink(_feed, "last");
				Assert.IsNotEmpty(rel);
			}

			[Test]
			public void the_feed_is_empty() {
				Assert.AreEqual(0, _feed["entries"].Count());
			}

			[Test]
			public void the_response_is_not_cachable() {
				Assert.AreEqual("max-age=0, no-cache, must-revalidate", _lastResponse.Headers["Cache-Control"]);
			}
		}


		[TestFixture, Category("LongRunning")]
		public class when_reading_a_stream_forward_with_deleted_linktos : HttpSpecificationWithLinkToToDeletedEvents {
			private JObject _feed;
			private List<JToken> _entries;

			protected override void When() {
				_feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/forward/10", accept: ContentType.Json);
				_entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
			}

			[Test]
			public void the_feed_has_one_event() {
				Assert.AreEqual(1, _entries.Count());
			}

			[Test]
			public void the_edit_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][0];
				Assert.AreEqual("edit", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
			}

			[Test]
			public void the_alt_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][1];
				Assert.AreEqual("alternate", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
			}
		}


		[TestFixture, Category("LongRunning")]
		public class when_reading_a_stream_forward_with_linkto : HttpSpecificationWithLinkToToEvents {
			private JObject _feed;
			private List<JToken> _entries;

			protected override void When() {
				_feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/forward/10", accept: ContentType.Json);
				_entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
			}

			[Test]
			public void the_feed_has_two_events() {
				Assert.AreEqual(2, _entries.Count());
			}

			[Test]
			public void the_second_edit_link_to_is_to_correct_uri() {
				var foo = _entries[1]["links"][0];
				Assert.AreEqual("edit", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + Stream2Name + "/0"), foo["uri"].ToString());
			}

			[Test]
			public void the_second_alt_link_to_is_to_correct_uri() {
				var foo = _entries[1]["links"][1];
				Assert.AreEqual("alternate", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + Stream2Name + "/0"), foo["uri"].ToString());
			}

			[Test]
			public void the_first_edit_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][0];
				Assert.AreEqual("edit", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + StreamName + "/1"), foo["uri"].ToString());
			}

			[Test]
			public void the_first_alt_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][1];
				Assert.AreEqual("alternate", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + StreamName + "/1"), foo["uri"].ToString());
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_reading_a_stream_forward_with_linkto_with_at_sign_in_name : HttpBehaviorSpecification {
			protected string LinkedStreamName;
			protected string StreamName;

			protected override void Given() {
				var creds = DefaultData.AdminCredentials;
				LinkedStreamName = Guid.NewGuid().ToString();
				StreamName = Guid.NewGuid() + "@" + Guid.NewGuid() + "@";
				using (var conn = TestConnection.Create(_node.TcpEndPoint)) {
					conn.ConnectAsync().Wait();
					conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, creds,
							new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
								new byte[0]))
						.Wait();
					conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, creds,
							new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
								new byte[0]))
						.Wait();
					conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, creds,
							new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
								new byte[0]))
						.Wait();
					conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
						new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo, false,
							Encoding.UTF8.GetBytes("0@" + StreamName), new byte[0])).Wait();
					conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
						new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo, false,
							Encoding.UTF8.GetBytes("1@" + StreamName), new byte[0])).Wait();
				}
			}

			private JObject _feed;
			private List<JToken> _entries;

			protected override void When() {
				_feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/forward/10", accept: ContentType.Json);
				_entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
			}

			[Test]
			public void the_feed_has_two_events() {
				Assert.AreEqual(2, _entries.Count());
			}

			[Test]
			public void the_second_edit_link_to_is_to_correct_uri() {
				var foo = _entries[1]["links"][0];
				Assert.AreEqual("edit", foo["relation"].ToString());
				//TODO GFY I really wish we were targeting 4.5 only so I could use Uri.EscapeDataString
				//given the choice between this and a dependency on system.web well yeah. When we have 4.5
				//only lets use Uri
				Assert.AreEqual(MakeUrl("/streams/" + StreamName.Replace("@", "%40") + "/0"), foo["uri"].ToString());
			}

			[Test]
			public void the_second_alt_link_to_is_to_correct_uri() {
				var foo = _entries[1]["links"][1];
				Assert.AreEqual("alternate", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + StreamName.Replace("@", "%40") + "/0"), foo["uri"].ToString());
			}

			[Test]
			public void the_first_edit_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][0];
				Assert.AreEqual("edit", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + StreamName.Replace("@", "%40") + "/1"), foo["uri"].ToString());
			}

			[Test]
			public void the_first_alt_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][1];
				Assert.AreEqual("alternate", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + StreamName.Replace("@", "%40") + "/1"), foo["uri"].ToString());
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			when_reading_a_stream_forward_with_maxcount_deleted_linktos :
				SpecificationWithLinkToToMaxCountDeletedEvents {
			private JObject _feed;
			private List<JToken> _entries;

			protected override void When() {
				_feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/forward/10", accept: ContentType.Json);
				_entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
			}

			[Test]
			public void the_feed_has_no_events() {
				Assert.AreEqual(1, _entries.Count());
			}
		}

		[TestFixture, Category("LongRunning")]
		[Explicit("Failing test for Greg demonstrating NullReferenceException in Convert.cs")]
		public class
			when_reading_a_stream_forward_with_maxcount_deleted_linktos_with_rich_entry :
				SpecificationWithLinkToToMaxCountDeletedEvents {
			private JObject _feed;
			private List<JToken> _entries;

			protected override void When() {
				_feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/forward/10?embed=rich",
					accept: ContentType.Json);
				_entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
			}

			[Test]
			public void the_feed_has_some_events() {
				Assert.AreEqual(1, _entries.Count());
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_reading_a_stream_forward_with_deleted_linktos_with_content_enabled_as_xml :
			HttpSpecificationWithLinkToToDeletedEvents {
			private XDocument _feed;
			private XElement[] _entries;

			protected override void When() {
				_feed = GetAtomXml(MakeUrl("/streams/" + LinkedStreamName + "/0/forward/10", "embed=content"));
				_entries = _feed.GetEntries();
			}

			[Test]
			public void the_feed_has_one_event() {
				Assert.AreEqual(1, _entries.Length);
			}

			[Test]
			public void the_edit_link_is_to_correct_uri() {
				var link = _entries[0].GetLink("edit");
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), link);
			}

			[Test]
			public void the_alternate_link_is_to_correct_uri() {
				var link = _entries[0].GetLink("alternate");
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), link);
			}
		}


		[TestFixture, Category("LongRunning")]
		public class
			when_reading_a_stream_forward_with_deleted_linktos_with_content_enabled :
				HttpSpecificationWithLinkToToDeletedEvents {
			private JObject _feed;
			private List<JToken> _entries;

			protected override void When() {
				var uri = MakeUrl("/streams/" + LinkedStreamName + "/0/forward/10", "embed=content");
				_feed = GetJson<JObject>(uri.ToString(), accept: ContentType.Json);
				_entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
			}

			[Test]
			public void the_feed_has_one_event() {
				Assert.AreEqual(1, _entries.Count());
			}

			[Test]
			public void the_edit_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][0];
				Assert.AreEqual("edit", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
			}

			[Test]
			public void the_alt_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][1];
				Assert.AreEqual("alternate", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
			}
		}


		[TestFixture, Category("LongRunning")]
		public class when_reading_a_stream_backward_with_deleted_linktos : HttpSpecificationWithLinkToToDeletedEvents {
			private JObject _feed;
			private List<JToken> _entries;

			protected override void When() {
				_feed = GetJson<JObject>("/streams/" + LinkedStreamName + "/0/backward/1", accept: ContentType.Json);
				_entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
			}

			[Test]
			public void the_feed_has_one_event() {
				Assert.AreEqual(1, _entries.Count());
			}

			[Test]
			public void the_edit_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][0];
				Assert.AreEqual("edit", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
			}

			[Test]
			public void the_alt_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][1];
				Assert.AreEqual("alternate", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
			}
		}


		[TestFixture, Category("LongRunning")]
		public class
			when_reading_a_stream_backward_with_deleted_linktos_and_embed_of_content :
				HttpSpecificationWithLinkToToDeletedEvents {
			private JObject _feed;
			private List<JToken> _entries;

			protected override void When() {
				var uri = MakeUrl("/streams/" + LinkedStreamName + "/0/backward/1", "embed=content");
				_feed = GetJson<JObject>(uri.ToString(), accept: ContentType.Json);
				_entries = _feed != null ? _feed["entries"].ToList() : new List<JToken>();
			}

			[Test]
			public void the_feed_has_one_event() {
				Assert.AreEqual(1, _entries.Count());
			}

			[Test]
			public void the_edit_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][0];
				Assert.AreEqual("edit", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
			}

			[Test]
			public void the_alt_link_to_is_to_correct_uri() {
				var foo = _entries[0]["links"][1];
				Assert.AreEqual("alternate", foo["relation"].ToString());
				Assert.AreEqual(MakeUrl("/streams/" + DeletedStreamName + "/0"), foo["uri"].ToString());
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_polling_the_head_forward_and_a_new_event_appears : SpecificationWithLongFeed {
			private JObject _feed;
			private JObject _head;
			private string _previous;
			private string _lastEventLocation;

			protected override void Given() {
				base.Given();
				_head = GetJson<JObject>(TestStream, ContentType.AtomJson);
				Console.WriteLine(new string('*', 60));
				Console.WriteLine(_head);
				Console.WriteLine(TestStream);
				Console.WriteLine(new string('*', 60));
				_previous = GetLink(_head, "previous");
				_lastEventLocation = PostEvent(-1);
			}

			protected override void When() {
				_feed = GetJson<JObject>(_previous, ContentType.AtomJson);
			}

			[Test]
			public void returns_ok_status_code() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void returns_a_feed_with_a_single_entry_referring_to_the_last_event() {
				HelperExtensions.AssertJson(new {entries = new[] {new {Id = _lastEventLocation}}}, _feed);
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			when_reading_a_stream_forward_from_beginning_asking_for_less_events_than_in_the_stream :
				SpecificationWithLongFeed {
			private JObject _feed;

			protected override void When() {
				_feed = GetJson<JObject>(TestStream + "/0/forward/" + (_numberOfEvents - 1),
					accept: ContentType.AtomJson);
			}

			[Test]
			public void the_head_of_stream_is_false() {
				Assert.AreEqual(false, _feed.Value<bool>("headOfStream"));
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			when_reading_a_stream_forward_from_beginning_asking_for_more_events_than_in_the_stream :
				SpecificationWithLongFeed {
			private JObject _feed;

			protected override void When() {
				_feed = GetJson<JObject>(TestStream + "/0/forward/" + (_numberOfEvents + 1),
					accept: ContentType.AtomJson);
			}

			[Test]
			public void the_head_of_stream_is_true() {
				Assert.AreEqual(true, _feed.Value<bool>("headOfStream"));
			}
		}

		[TestFixture, Category("LongRunning")]
		public class
			when_reading_a_stream_forward_from_beginning_asking_for_exactly_the_events_in_the_stream :
				SpecificationWithLongFeed {
			private JObject _feed;

			protected override void When() {
				_feed = GetJson<JObject>(TestStream + "/0/forward/" + _numberOfEvents, accept: ContentType.AtomJson);
			}

			[Test]
			public void the_head_of_stream_is_true() {
				Assert.AreEqual(true, _feed.Value<bool>("headOfStream"));
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_reading_a_stream_forward_with_etag_in_header : SpecificationWithLongFeed {
			private JObject _feed;

			protected override void When() {
				_feed = GetJson<JObject>(TestStream, accept: ContentType.AtomJson);
				var etag = _feed.Value<string>("eTag");
				var headers = new NameValueCollection();
				headers.Add("If-None-Match", etag);
				_feed = GetJson<JObject>(TestStream, accept: ContentType.AtomJson, headers: headers);
			}

			[Test]
			public void should_return_not_modified() {
				Assert.AreEqual(HttpStatusCode.NotModified, _lastResponse.StatusCode);
			}
		}
	}
}

// This test needs to be out of the streams namespace to prevent it from inheriting the wrong mini node.
namespace EventStore.Core.Tests.Http {
	public class when_running_the_node_advertising_a_different_ip_as {
		[TestFixture, Category("LongRunning")]
		public class when_retrieving_feed_head_and_http_advertise_ip_is_set : HttpBehaviorSpecification {
			private JObject _feed;
			private IPAddress advertisedAddress = IPAddress.Parse("192.168.10.1");
			private int advertisedPort = 2116;

			protected override MiniNode CreateMiniNode() {
				return new MiniNode(PathName, skipInitializeStandardUsersCheck: GivenSkipInitializeStandardUsersCheck(),
					advertisedExtIPAddress: advertisedAddress, advertisedExtHttpPort: advertisedPort);
			}

			protected override void Given() {
				var response = MakeArrayEventsPost(
					TestStream,
					new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {Number = 1}}});
				Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
			}

			protected string GetLink(JObject feed, string relation) {
				var rel = (from JObject link in feed["links"]
					from JProperty attr in link
					where attr.Name == "relation" && (string)attr.Value == relation
					select link).SingleOrDefault();
				return (rel == null) ? (string)null : (string)rel["uri"];
			}

			protected string GetFirstEntryLink(JObject feed) {
				var rel = (from JObject entry in feed["entries"]
					from JObject link in entry["links"]
					from JProperty attr in link
					where attr.Name == "relation" && (string)attr.Value == "edit"
					select link).FirstOrDefault();
				return (rel == null) ? (string)null : (string)rel["uri"];
			}

			protected override void When() {
				Console.WriteLine("Getting feed");
				_feed = GetJson<JObject>(TestStream, ContentType.AtomJson);
				Console.WriteLine("Feed: {0}", _feed);
			}

			[Test]
			public void returns_ok_status_code() {
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
			}

			[Test]
			public void contains_a_link_rel_previous_using_advertised_ip_and_port() {
				var rel = GetLink(_feed, "previous");
				Assert.IsNotEmpty(rel);
				var uri = new Uri(rel);
				Assert.AreEqual(advertisedAddress.ToString(), uri.Host);
				Assert.AreEqual(advertisedPort, uri.Port);
			}

			[Test]
			public void contains_a_link_rel_self_using_advertised_ip_and_port() {
				var rel = GetLink(_feed, "self");
				Assert.IsNotEmpty(rel);
				var uri = new Uri(rel);
				Assert.AreEqual(advertisedAddress.ToString(), uri.Host);
				Assert.AreEqual(advertisedPort, uri.Port);
			}

			[Test]
			public void contains_a_link_rel_first_using_advertised_ip_and_port() {
				var rel = GetLink(_feed, "first");
				Assert.IsNotEmpty(rel);
				var uri = new Uri(rel);
				Assert.AreEqual(advertisedAddress.ToString(), uri.Host);
				Assert.AreEqual(advertisedPort, uri.Port);
			}

			[Test]
			public void contains_an_entry_with_rel_link_using_advertised_ip_and_port() {
				var rel = GetFirstEntryLink(_feed);
				Assert.IsNotEmpty(rel);
				var uri = new Uri(rel);
				Assert.AreEqual(advertisedAddress.ToString(), uri.Host);
				Assert.AreEqual(advertisedPort, uri.Port);
			}
		}
	}
}
