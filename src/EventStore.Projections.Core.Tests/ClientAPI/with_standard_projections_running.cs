using System;
using System.Text;
using EventStore.ClientAPI;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Services.Processing;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI {
	namespace with_standard_projections_running {
		public abstract class when_deleting_stream_base : specification_with_standard_projections_runnning {
			[Test, Category("Network")]
			public void streams_stream_exists() {
				Assert.AreEqual(
					SliceReadStatus.Success,
					_conn.ReadStreamEventsForwardAsync("$streams", 0, 10, false, _admin).Result.Status);
			}

			[Test, Category("Network")]
			public void deleted_stream_events_are_indexed() {
				var slice = _conn.ReadStreamEventsForwardAsync("$ce-cat", 0, 10, true, _admin).Result;
				Assert.AreEqual(SliceReadStatus.Success, slice.Status);

				Assert.AreEqual(3, slice.Events.Length);
				var deletedLinkMetadata = slice.Events[2].Link.Metadata;
				Assert.IsNotNull(deletedLinkMetadata);

				var checkpointTag = Encoding.UTF8.GetString(deletedLinkMetadata).ParseCheckpointExtraJson();
				JToken deletedValue;
				Assert.IsTrue(checkpointTag.TryGetValue("$deleted", out deletedValue));
				JToken originalStream;
				Assert.IsTrue(checkpointTag.TryGetValue("$o", out originalStream));
				Assert.AreEqual("cat-1", ((JValue)originalStream).Value);
			}

			[Test, Category("Network")]
			public void deleted_stream_events_are_indexed_as_deleted() {
				var slice = _conn.ReadStreamEventsForwardAsync("$et-$deleted", 0, 10, true, _admin).Result;
				Assert.AreEqual(SliceReadStatus.Success, slice.Status);

				Assert.AreEqual(1, slice.Events.Length);
			}

			protected override void When() {
				base.When();
				var r1 = _conn.AppendToStreamAsync(
						"cat-1", ExpectedVersion.NoStream, _admin,
						new EventData(Guid.NewGuid(), "type1", true, Encoding.UTF8.GetBytes("{}"), null))
					.Result;

				var r2 = _conn.AppendToStreamAsync(
						"cat-1", r1.NextExpectedVersion, _admin,
						new EventData(Guid.NewGuid(), "type1", true, Encoding.UTF8.GetBytes("{}"), null))
					.Result;

				_conn.DeleteStreamAsync("cat-1", r2.NextExpectedVersion, GivenDeleteHardDeleteStreamMode(), _admin)
					.Wait();
				QueueStatsCollector.WaitIdle();
				if (!GivenStandardProjectionsRunning()) {
					EnableStandardProjections();
					WaitIdle();
				}
			}

			protected abstract bool GivenDeleteHardDeleteStreamMode();
		}

		[TestFixture]
		public class when_hard_deleting_stream : when_deleting_stream_base {
			protected override bool GivenDeleteHardDeleteStreamMode() {
				return true;
			}
		}

		[TestFixture]
		public class when_soft_deleting_stream : when_deleting_stream_base {
			protected override bool GivenDeleteHardDeleteStreamMode() {
				return false;
			}
		}

		[TestFixture]
		public class when_hard_deleting_stream_and_starting_standard_projections : when_deleting_stream_base {
			protected override bool GivenDeleteHardDeleteStreamMode() {
				return true;
			}

			protected override bool GivenStandardProjectionsRunning() {
				return false;
			}
		}

		[TestFixture]
		public class when_soft_deleting_stream_and_starting_standard_projections : when_deleting_stream_base {
			protected override bool GivenDeleteHardDeleteStreamMode() {
				return false;
			}

			protected override bool GivenStandardProjectionsRunning() {
				return false;
			}
		}
	}
}
