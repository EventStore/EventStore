using System;
using System.Linq;
using KellermanSoftware.CompareNetObjects;
using Xunit;

namespace EventStore.Grpc.Streams {
	internal static class AssertEx {
		public static void EventsEqual(EventData[] expected, EventRecord[] actual) {
			if (expected == null) throw new ArgumentNullException(nameof(expected));
			if (actual == null) throw new ArgumentNullException(nameof(actual));
			var logic = new CompareLogic {
				Config = {
					MaxDifferences = 16
				}
			};

			var expectedObject = Array.ConvertAll(expected, e => new {
				e.EventId,
				e.Type,
				e.IsJson,
				e.Data,
				e.Metadata
			});
			var actualObject = Array.ConvertAll(actual, e => new {
				e.EventId,
				Type = e.EventType,
				e.IsJson,
				e.Data,
				e.Metadata
			});
			var result = logic.Compare(
				expectedObject,
				actualObject);

			Assert.True(result.AreEqual, result.DifferencesString);
		}
	}
}
