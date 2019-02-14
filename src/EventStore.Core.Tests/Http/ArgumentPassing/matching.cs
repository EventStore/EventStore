using System;
using System.Net;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Http.ArgumentPassing {
	namespace matching {
		[TestFixture, Category("LongRunning")]
		class when_matching_against_simple_placeholders : HttpBehaviorSpecification {
			private JObject _response;

			protected override void Given() {
			}

			protected override void When() {
			}

			[Test]
			[TestCase("1", "1", "2", "2")]
			[TestCase("1", "1", "%41", "A")]
			[TestCase("1", "1", "$", "$")]
			[TestCase("1", "1", "%24", "$")]
			[TestCase("%24", "$", "2", "2")]
			[TestCase("$", "$", "2", "2")]
			[TestCase("$", "$", "йцукен", "йцукен")]
			[TestCase("$", "$", "%D0%B9%D1%86%D1%83%D0%BA%D0%B5%D0%BD", "йцукен")]
			[TestCase("йцукен", "йцукен", "2", "2")]
			[TestCase("%D0%B9%D1%86%D1%83%D0%BA%D0%B5%D0%BD", "йцукен", "2", "2")]
//            [TestCase("%3F", "?", "2", "2")] // ?
//            [TestCase("%2F", "/", "2", "2")] // /
			[TestCase("%20", " ", "2", "2")] // space
			[TestCase("%25", "%", "2", "2")] // %
			public void returns_ok_status_code(string _a, string _ra, string _b, string _rb) {
				_response = GetJson2<JObject>("/test-encoding/" + _a, "b=" + _b);
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
				HelperExtensions.AssertJson(new {a = _ra, b = _rb}, _response);
			}
		}

		[TestFixture, Category("LongRunning")]
		[Ignore("Only demonstrates differences between .NET and Mono")]
		class when_matching_against_placeholders_with_reserved_characters : HttpBehaviorSpecification {
			private JObject _response;

			protected override void Given() {
			}

			protected override void When() {
			}

			[Test]
			// [TestCase("%24", "$", "2", "2")]
			// [TestCase("$", "$", "2", "2")]
			// [TestCase("%3F", "?", "2", "2")] // ?
			// [TestCase("%2F", "/", "2", "2")] // /
			// [TestCase("%20", " ", "2", "2")] // space
			// [TestCase("%25", "%", "2", "2")] // %
			public void returns_ok_status_code(string _a, string _ra, string _b, string _rb) {
				_response = GetJson2<JObject>("/test-encoding-reserved-" + _a, "?b=" + _b);
				Assert.AreEqual(HttpStatusCode.OK, _lastResponse.StatusCode);
				Console.WriteLine(_response.ToString());
				HelperExtensions.AssertJson(new {a = _ra, b = _rb}, _response);
			}
		}
	}
}
