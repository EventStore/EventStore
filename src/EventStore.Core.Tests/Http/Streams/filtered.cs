using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Core.Tests.Http.Users.users;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using HttpStatusCode = System.Net.HttpStatusCode;
using EventStore.Transport.Http;

namespace EventStore.Core.Tests.Http.Streams {
	public class filtered {
		public abstract class SpecificationWithLongFeed : with_admin_user {
			protected int NumberOfEvents;

			protected override void Given() {
				NumberOfEvents = 25;
				for (var i = 0; i < NumberOfEvents; i++) {
					PostEvent(i, TestStream + "-ignore", "ignore-event-type");
					PostEvent(i, TestStream + "-filter", "event1-type");
					PostEvent(i, TestStream + "-filter", "event2-type");
				}
			}

			protected string PostEvent(int i, string streamId, string eventType) {
				var response = MakeArrayEventsPost(
					streamId,
					new[] {new {EventId = Guid.NewGuid(), EventType = eventType, Data = new {Number = i}}});
				Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
				return response.Headers[HttpResponseHeader.Location];
			}

			protected string GetLink(JObject feed, string relation) {
				var rel = (from JObject link in feed["links"]
					from JProperty attr in link
					where attr.Name == "relation" && (string)attr.Value == relation
					select link).SingleOrDefault();
				return (rel == null) ? null : (string)rel["uri"];
			}

			protected List<string> GetEventTypes(JObject feed) =>
				feed["entries"]
					.Select(e => e["summary"].Value<string>())
					.OrderBy(e => e)
					.ToList();

			protected string AllFilteredStream => "/streams/%24all/filtered";
		
			protected string AllFilteredStreamForward => "/streams/$all/filtered/00000000000000000000000000000000/forward/14";
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_backward_with_invalid_context : SpecificationWithLongFeed {
			protected override void When() =>
				GetJson<JObject>(
					AllFilteredStream + "?context=foo",
					ContentType.AtomJson,
					DefaultData.AdminNetworkCredentials);


			[Test]
			public void returns_bad_request_status_code() =>
				Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);

			[Test]
			public void returns_status_description() =>
				Assert.AreEqual(
					"Invalid context please provide one of the following: StreamId, EventType.",
					_lastResponse.StatusDescription);
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_backward_with_invalid_type : SpecificationWithLongFeed {
			protected override void When() =>
				GetJson<JObject>($"{AllFilteredStream}?context=streamid&type=foo",
					ContentType.AtomJson,
					DefaultData.AdminNetworkCredentials);

			[Test]
			public void returns_bad_request_status_code() =>
				Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);

			[Test]
			public void returns_status_description() =>
				Assert.AreEqual(
					"Invalid type please provide one of the following: Regex, Prefix.",
					_lastResponse.StatusDescription);
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_backward_with_invalid_data : SpecificationWithLongFeed {
			protected override void When() =>
				GetJson<JObject>($"{AllFilteredStream}?context=streamid&type=prefix",
					ContentType.AtomJson,
					DefaultData.AdminNetworkCredentials);

			[Test]
			public void returns_bad_request_status_code() =>
				Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);

			[Test]
			public void returns_status_description() =>
				Assert.AreEqual(
					"Please provide a comma delimited list of data with at least one item.",
					_lastResponse.StatusDescription);
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_backward_feed_head : SpecificationWithLongFeed {
			private JObject _feed;

			protected override void When() =>
				_feed = GetJson<JObject>($"{AllFilteredStream}?context=eventtype&type=prefix&data=event1-",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);

			[Test]
			public void returns_ok_status_code() =>
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

			[Test]
			public void contains_a_link_rel_previous() {
				var rel = GetLink(_feed, "previous");
				Assert.IsNotEmpty(rel);
				Assert.AreEqual(
					MakeUrl($"{AllFilteredStream}/0000000000002CBC0000000000002CBC/forward/20"),
					new Uri(rel));
			}

			[Test]
			public void contains_a_link_rel_next() {
				var rel = GetLink(_feed, "next");
				Assert.IsNotEmpty(rel);
				Assert.AreEqual(
					MakeUrl($"{AllFilteredStream}/00000000000022480000000000002248/backward/20"),
					new Uri(rel));
			}

			[Test]
			public void contains_a_link_rel_self() {
				var rel = GetLink(_feed, "self");
				Assert.IsNotEmpty(rel);
				Assert.AreEqual(
					MakeUrl(AllFilteredStream),
					new Uri(rel));
			}

			[Test]
			public void contains_a_link_rel_first() {
				var rel = GetLink(_feed, "first");
				Assert.IsNotEmpty(rel);
				Assert.AreEqual(
					MakeUrl($"{AllFilteredStream}/head/backward/20"),
					new Uri(rel));
			}

			[Test]
			public void contains_a_link_rel_last() {
				var rel = GetLink(_feed, "last");
				Assert.IsNotEmpty(rel);
				Assert.AreEqual(
					MakeUrl($"{AllFilteredStream}/00000000000000000000000000000000/forward/20"),
					new Uri(rel));
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_backward_feed_events_by_event_type_and_prefix : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>($"{AllFilteredStream}?context=eventtype&type=prefix&data=event1-,event2-",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void should_only_contain_filtered_events() {
				for (var index = 0; index < 7; index++) {
					Assert.AreEqual("event1-type", _eventTypes[index]);
					Assert.AreEqual("event2-type", _eventTypes[index + 7]);
				}
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_backward_feed_events_by_event_type_and_regex : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>($"{AllFilteredStream}?context=eventtype&type=regex&data=^.*eventtype1.*$",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void should_only_contain_filtered_events() {
				foreach (var eventType in _eventTypes) {
					Assert.AreEqual("event1-type", eventType);
				}
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_backward_feed_events_by_stream_id_and_prefix : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>(
					$"{AllFilteredStream}?context=streamid&type=prefix&data={TestStream}-filter",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void should_only_contain_filtered_events() {
				foreach (var eventType in _eventTypes) {
					Assert.AreEqual("event-type", eventType);
				}
			}
		}
		
		[TestFixture, Category("LongRunning")]
		public class when_retrieving_backward_feed_events_by_stream_id_and_regex : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>($"{AllFilteredStream}?context=streamid&type=regex&data=^.*{TestStream}-filter.*$",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void should_only_contain_filtered_events() {
				foreach (var eventType in _eventTypes) {
					Assert.AreEqual("event1-type", eventType);
				}
			}
		}
		
		[TestFixture, Category("LongRunning")]
		public class when_retrieving_backward_feed_events_filtering_system_events : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>($"{AllFilteredStream}?exclude-system-events=true",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void should_only_contain_filtered_events() {
				foreach (var eventType in _eventTypes) {
					Assert.IsFalse(eventType.StartsWith("$"));
				}
			}
		}
		
				[TestFixture, Category("LongRunning")]
		public class when_retrieving_forward_with_invalid_context : SpecificationWithLongFeed {
			protected override void When() =>
				GetJson<JObject>(
					AllFilteredStreamForward + "?context=foo",
					ContentType.AtomJson,
					DefaultData.AdminNetworkCredentials);


			[Test]
			public void returns_bad_request_status_code() =>
				Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);

			[Test]
			public void returns_status_description() =>
				Assert.AreEqual(
					"Invalid context please provide one of the following: StreamId, EventType.",
					_lastResponse.StatusDescription);
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_forward_with_invalid_type : SpecificationWithLongFeed {
			protected override void When() =>
				GetJson<JObject>($"{AllFilteredStreamForward}?context=streamid&type=foo",
					ContentType.AtomJson,
					DefaultData.AdminNetworkCredentials);

			[Test]
			public void returns_bad_request_status_code() =>
				Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);

			[Test]
			public void returns_status_description() =>
				Assert.AreEqual(
					"Invalid type please provide one of the following: Regex, Prefix.",
					_lastResponse.StatusDescription);
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_forward_with_invalid_data : SpecificationWithLongFeed {
			protected override void When() =>
				GetJson<JObject>($"{AllFilteredStreamForward}?context=streamid&type=prefix",
					ContentType.AtomJson,
					DefaultData.AdminNetworkCredentials);

			[Test]
			public void returns_bad_request_status_code() =>
				Assert.AreEqual(HttpStatusCode.BadRequest, _lastResponse.StatusCode);

			[Test]
			public void returns_status_description() =>
				Assert.AreEqual(
					"Please provide a comma delimited list of data with at least one item.",
					_lastResponse.StatusDescription);
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_forward_feed_head : SpecificationWithLongFeed {
			private JObject _feed;

			protected override void When() =>
				_feed = GetJson<JObject>($"{AllFilteredStreamForward}?context=eventtype&type=prefix&data=event1-",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);

			[Test]
			public void returns_ok_status_code() =>
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);

			[Test]
			public void contains_a_link_rel_previous() {
				var rel = GetLink(_feed, "previous");
				Assert.IsNotEmpty(rel);
				Assert.AreEqual(
					MakeUrl($"{AllFilteredStream}/0000000000001AF20000000000000000/forward/14"),
					new Uri(rel));
			}


			[Test]
			public void contains_a_link_rel_self() {
				var rel = GetLink(_feed, "self");
				Assert.IsNotEmpty(rel);
				Assert.AreEqual(
					MakeUrl(AllFilteredStream),
					new Uri(rel));
			}

			[Test]
			public void contains_a_link_rel_first() {
				var rel = GetLink(_feed, "first");
				Assert.IsNotEmpty(rel);
				Assert.AreEqual(
					MakeUrl($"{AllFilteredStream}/head/backward/14"),
					new Uri(rel));
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_forward_feed_events_by_event_type_and_prefix : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>($"{AllFilteredStreamForward}?context=eventtype&type=prefix&data=event1-,event2-",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void  should_only_contain_filtered_events() {
				for (var index = 0; index < 7; index++) {
					Assert.AreEqual("event1-type", _eventTypes[index]);
					Assert.AreEqual("event2-type", _eventTypes[index + 7]);
				}
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_forward_feed_events_by_event_type_and_regex : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>($"{AllFilteredStreamForward}?context=eventtype&type=regex&data=^.*eventtype1.*$",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void should_only_contain_filtered_events() {
				foreach (var eventType in _eventTypes) {
					Assert.AreEqual("event1-type", eventType);
				}
			}
		}

		[TestFixture, Category("LongRunning")]
		public class when_retrieving_forward_feed_events_by_stream_id_and_prefix : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>(
					$"{AllFilteredStreamForward}?context=streamid&type=prefix&data={TestStream}-filter",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void should_only_contain_filtered_events() {
				foreach (var eventType in _eventTypes) {
					Assert.AreEqual("event-type", eventType);
				}
			}
		}
		
		[TestFixture, Category("LongRunning")]
		public class when_retrieving_forward_feed_events_by_stream_id_and_regex : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>($"{AllFilteredStreamForward}?context=streamid&type=regex&data=^.*{TestStream}-filter.*$",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void should_only_contain_filtered_events() {
				foreach (var eventType in _eventTypes) {
					Assert.AreEqual("event1-type", eventType);
				}
			}
		}
		
		[TestFixture, Category("LongRunning")]
		public class when_retrieving_forward_feed_events_filtering_system_events : SpecificationWithLongFeed {
			private List<string> _eventTypes;

			protected override void When() {
				var feed = GetJson<JObject>($"{AllFilteredStreamForward}?exclude-system-events=true",
					ContentType.AtomJson, DefaultData.AdminNetworkCredentials);
				_eventTypes = GetEventTypes(feed);
			}

			[Test]
			public void should_only_contain_filtered_events() {
				foreach (var eventType in _eventTypes) {
					Assert.IsFalse(eventType.StartsWith("$"));
				}
			}
		}
	}
}
