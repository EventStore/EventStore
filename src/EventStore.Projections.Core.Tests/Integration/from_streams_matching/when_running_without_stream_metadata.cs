using System.Collections.Generic;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Integration.from_streams_matching {
	[TestFixture]
	public class when_running_without_stream_metadata : specification_with_a_v8_query_posted {
		protected override void GivenEvents() {
			ExistingEvent("stream1", "event", "{}", "{\"data\":1}", isJson: true);
			ExistingEvent("stream2", "event", "{}", "{\"data\":2}", isJson: true);
			ExistingEvent("stream2", "event", "{}", "{\"data\":21}", isJson: true);
			ExistingEvent("stream3", "event", "{}", "{\"data\":3}", isJson: true);
			ExistingEvent("other1", "other-event", "{}", "{\"data\":1}", isJson: true);
			ExistingEvent("other2", "other-event", "{}", "{\"data\":2}", isJson: true);
		}

		protected override IEnumerable<WhenStep> When() {
			foreach (var e in base.When()) yield return e;
		}

		protected override bool GivenInitializeSystemProjections() {
			return true;
		}

		protected override bool GivenStartSystemProjections() {
			return true;
		}

		protected override string GivenQuery() {
			return @"
fromStreamsMatching(function(s, streamMeta){
        return s.indexOf(""stream"") === 0;
}).when({
    $any: function(s, e) {
        return {data: e.data.data};
    }
})";
		}

		[Test]
		public void query_returns_correct_result() {
			AssertStreamTailWithLinks(
				"$projections-query-result", @"Result:{""data"":1}", @"Result:{""data"":21}", @"Result:{""data"":3}",
				"$Eof:");
		}
	}
}
