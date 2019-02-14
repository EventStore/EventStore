using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Projections;
using EventStore.Common.Utils;

namespace EventStore.Projections.Core.Tests.ClientAPI.projectionsManager {
	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_creating_one_time_projection : SpecificationWithNodeAndProjectionsManager {
		private string _streamName;
		private string _query;

		public override void Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateStandardQuery(_streamName);
		}

		public override void When() {
			_projManager.CreateOneTimeAsync(_query, _credentials).Wait();
		}

		[Test]
		public void should_create_projection() {
			var projections = _projManager.ListOneTimeAsync(_credentials).Result;
			Assert.AreEqual(1, projections.Count);
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_creating_transient_projection : SpecificationWithNodeAndProjectionsManager {
		private string _streamName;
		private string _projectionName;
		private string _query;

		public override void Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_creating_transient_projection";
			PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateStandardQuery(_streamName);
		}

		public override void When() {
			_projManager.CreateTransientAsync(_projectionName, _query, _credentials).Wait();
		}

		[Test]
		public void should_create_projection() {
			var status = _projManager.GetStatusAsync(_projectionName, _credentials).Result;
			Assert.IsNotEmpty(status);
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_creating_continuous_projection : SpecificationWithNodeAndProjectionsManager {
		private string _streamName;
		private string _emittedStreamName;
		private string _projectionName;
		private string _query;
		private string _projectionId;

		public override void Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_creating_continuous_projection";
			_emittedStreamName = "emittedStream-" + Guid.NewGuid().ToString();
			PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateEmittingQuery(_streamName, _emittedStreamName);
		}

		public override void When() {
			_projManager.CreateContinuousAsync(_projectionName, _query, _credentials).Wait();
		}

		[Test]
		public void should_create_projection() {
			var allProjections = _projManager.ListContinuousAsync(_credentials).Result;
			var proj = allProjections.FirstOrDefault(x => x.EffectiveName == _projectionName);
			_projectionId = proj.Name;
			Assert.IsNotNull(proj);
		}

		[Test]
		public void should_have_turn_on_emit_to_stream() {
			var events = _connection
				.ReadEventAsync(string.Format("$projections-{0}", _projectionId), 0, true, _credentials).Result;
			var data = System.Text.Encoding.UTF8.GetString(events.Event.Value.Event.Data);
			var eventData = data.ParseJson<JObject>();
			Assert.IsTrue((bool)eventData["emitEnabled"]);
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class
		when_creating_continuous_projection_with_track_emitted_streams : SpecificationWithNodeAndProjectionsManager {
		private string _streamName;
		private string _emittedStreamName;
		private string _projectionName;
		private string _query;
		private string _projectionId;

		public override void Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_creating_continuous_projection_with_track_emitted_streams";
			_emittedStreamName = "emittedStream-" + Guid.NewGuid().ToString();
			PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");

			_query = CreateEmittingQuery(_streamName, _emittedStreamName);
		}

		public override void When() {
			_projManager.CreateContinuousAsync(_projectionName, _query, true, _credentials).Wait();
		}

		[Test]
		public void should_create_projection() {
			var allProjections = _projManager.ListContinuousAsync(_credentials).Result;
			var proj = allProjections.FirstOrDefault(x => x.EffectiveName == _projectionName);
			_projectionId = proj.Name;
			Assert.IsNotNull(proj);
		}

		[Test]
		public void should_enable_track_emitted_streams() {
			var events = _connection
				.ReadEventAsync(string.Format("$projections-{0}", _projectionId), 0, true, _credentials).Result;
			var data = System.Text.Encoding.UTF8.GetString(events.Event.Value.Event.Data);
			var eventData = data.ParseJson<JObject>();
			Assert.IsTrue((bool)eventData["trackEmittedStreams"]);
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_disabling_projections : SpecificationWithNodeAndProjectionsManager {
		private string _streamName;
		private string _projectionName;
		private string _query;

		public override void Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_disabling_projection";
			PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateStandardQuery(_streamName);

			_projManager.CreateContinuousAsync(_projectionName, _query, _credentials).Wait();
		}

		public override void When() {
			_projManager.DisableAsync(_projectionName, _credentials).Wait();
		}

		[Test]
		public void should_stop_the_projection() {
			var projectionStatus = _projManager.GetStatusAsync(_projectionName, _credentials).Result;
			var status = projectionStatus.ParseJson<JObject>()["status"].ToString();
			Assert.IsTrue(status.Contains("Stopped"));
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_enabling_projections : SpecificationWithNodeAndProjectionsManager {
		private string _streamName;
		private string _projectionName;
		private string _query;

		public override void Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_enabling_projections";
			PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateStandardQuery(_streamName);

			_projManager.CreateContinuousAsync(_projectionName, _query, _credentials).Wait();
			_projManager.DisableAsync(_projectionName, _credentials).Wait();
		}

		public override void When() {
			_projManager.EnableAsync(_projectionName, _credentials).Wait();
		}

		[Test]
		public void should_reenable_projection() {
			var projectionStatus = _projManager.GetStatusAsync(_projectionName, _credentials).Result;
			var status = projectionStatus.ParseJson<JObject>()["status"].ToString();
			Assert.IsTrue(status.Contains("Running"));
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_listing_the_projections : SpecificationWithNodeAndProjectionsManager {
		private List<ProjectionDetails> _result;

		public override void Given() {
			CreateContinuousProjection(Guid.NewGuid().ToString());
		}

		public override void When() {
			_result = _projManager.ListAllAsync(_credentials).Result.ToList();
		}

		[Test]
		public void should_return_all_projections() {
			Assert.IsNotEmpty(_result);
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_listing_one_time_projections : SpecificationWithNodeAndProjectionsManager {
		private List<ProjectionDetails> _result;

		public override void Given() {
			CreateOneTimeProjection();
		}

		public override void When() {
			_result = _projManager.ListOneTimeAsync(_credentials).Result.ToList();
		}

		[Test]
		public void should_return_projections() {
			Assert.IsNotEmpty(_result);
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_listing_continuous_projections : SpecificationWithNodeAndProjectionsManager {
		private List<ProjectionDetails> _result;
		private string _projectionName;

		public override void Given() {
			_projectionName = Guid.NewGuid().ToString();
			CreateContinuousProjection(_projectionName);
		}

		public override void When() {
			_result = _projManager.ListContinuousAsync(_credentials).Result.ToList();
		}

		[Test]
		public void should_return_continuous_projections() {
			Assert.IsTrue(_result.Any(x => x.EffectiveName == _projectionName));
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_a_projection_is_running : SpecificationWithNodeAndProjectionsManager {
		private string _projectionName;
		private string _streamName;
		private string _query;

		public override void Given() {
			_projectionName = "when_getting_projection_information";
			_streamName = "test-stream-" + Guid.NewGuid().ToString();

			PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");
		}

		public override void When() {
			_query = CreateStandardQuery(_streamName);
			_projManager.CreateContinuousAsync(_projectionName, _query, _credentials).Wait();
		}

		[Test]
		public void should_be_able_to_get_the_projection_state() {
			var state = _projManager.GetStateAsync(_projectionName, _credentials).Result;
			Assert.IsNotEmpty(state);
		}

		[Test]
		public void should_be_able_to_get_the_projection_status() {
			var status = _projManager.GetStatusAsync(_projectionName, _credentials).Result;
			Assert.IsNotEmpty(status);
		}

		[Test]
		public void should_be_able_to_get_the_projection_result() {
			var result = _projManager.GetResultAsync(_projectionName, _credentials).Result;
			Assert.AreEqual("{\"count\":1}", result);
		}

		[Test]
		public void should_be_able_to_get_the_projection_query() {
			var query = _projManager.GetQueryAsync(_projectionName, _credentials).Result;
			Assert.AreEqual(_query, query);
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_updating_a_projection_query : SpecificationWithNodeAndProjectionsManager {
		private string _projectionName;
		private string _streamName;
		private string _newQuery;

		public override void Given() {
			_projectionName = "when_updating_a_projection_query";
			_streamName = "test-stream-" + Guid.NewGuid().ToString();

			PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			var origQuery = CreateStandardQuery(_streamName);
			_newQuery = CreateStandardQuery("DifferentStream");
			_projManager.CreateContinuousAsync(_projectionName, origQuery, _credentials).Wait();
		}

		public override void When() {
			_projManager.UpdateQueryAsync(_projectionName, _newQuery, _credentials).Wait();
		}

		[Test]
		public void should_update_the_projection_query() {
			var query = _projManager.GetQueryAsync(_projectionName, _credentials).Result;
			Assert.AreEqual(_newQuery, query);
		}
	}
}
