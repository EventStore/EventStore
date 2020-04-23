using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

		public override async Task Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			await PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			await PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateStandardQuery(_streamName);
		}

		public override Task When() {
			return _projManager.CreateOneTimeAsync(_query, _credentials);
		}

		[Test]
		public async Task should_create_projection() {
			var projections = await _projManager.ListOneTimeAsync(_credentials);
			Assert.AreEqual(1, projections.Count);
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_creating_transient_projection : SpecificationWithNodeAndProjectionsManager {
		private string _streamName;
		private string _projectionName;
		private string _query;

		public override async Task Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_creating_transient_projection";
			await PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			await PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateStandardQuery(_streamName);
		}

		public override Task When() {
			return _projManager.CreateTransientAsync(_projectionName, _query, _credentials);
		}

		[Test]
		public async Task should_create_projection() {
			var status = await _projManager.GetStatusAsync(_projectionName, _credentials);
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

		public override async Task Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_creating_continuous_projection";
			_emittedStreamName = "emittedStream-" + Guid.NewGuid().ToString();
			await PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			await PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateEmittingQuery(_streamName, _emittedStreamName);
		}

		public override Task When() {
			return _projManager.CreateContinuousAsync(_projectionName, _query, _credentials);
		}

		[Test]
		public async Task should_create_projection() {
			var allProjections = await _projManager.ListContinuousAsync(_credentials);
			var proj = allProjections.FirstOrDefault(x => x.EffectiveName == _projectionName);
			_projectionId = proj.Name;
			Assert.IsNotNull(proj);
		}

		[Test]
		public async Task should_have_turn_on_emit_to_stream() {
			var events = await _connection
				.ReadEventAsync(string.Format("$projections-{0}", _projectionId), 0, true, _credentials);
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

		public override async Task Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_creating_continuous_projection_with_track_emitted_streams";
			_emittedStreamName = "emittedStream-" + Guid.NewGuid().ToString();
			await PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");

			_query = CreateEmittingQuery(_streamName, _emittedStreamName);
		}

		public override Task When() {
			return _projManager.CreateContinuousAsync(_projectionName, _query, true, _credentials);
		}

		[Test]
		public async Task should_create_projection() {
			var allProjections = await _projManager.ListContinuousAsync(_credentials);
			var proj = allProjections.FirstOrDefault(x => x.EffectiveName == _projectionName);
			_projectionId = proj.Name;
			Assert.IsNotNull(proj);
		}

		[Test]
		public async Task should_enable_track_emitted_streams() {
			var events = await _connection
				.ReadEventAsync(string.Format("$projections-{0}", _projectionId), 0, true, _credentials);
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

		public override async Task Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_disabling_projection";
			await PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			await PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateStandardQuery(_streamName);

			await _projManager.CreateContinuousAsync(_projectionName, _query, _credentials);
		}

		public override Task When() {
			return _projManager.DisableAsync(_projectionName, _credentials);
		}

		[Test]
		public async Task should_stop_the_projection() {
			var projectionStatus = await _projManager.GetStatusAsync(_projectionName, _credentials);
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

		public override async Task Given() {
			_streamName = "test-stream-" + Guid.NewGuid().ToString();
			_projectionName = "when_enabling_projections";
			await PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			await PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			_query = CreateStandardQuery(_streamName);

			await _projManager.CreateContinuousAsync(_projectionName, _query, _credentials);
			await _projManager.DisableAsync(_projectionName, _credentials);
		}

		public override Task When() {
			return _projManager.EnableAsync(_projectionName, _credentials);
		}

		[Test]
		public async Task should_reenable_projection() {
			var projectionStatus = await _projManager.GetStatusAsync(_projectionName, _credentials);
			var status = projectionStatus.ParseJson<JObject>()["status"].ToString();
			Assert.IsTrue(status.Contains("Running"));
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_listing_the_projections : SpecificationWithNodeAndProjectionsManager {
		private List<ProjectionDetails> _result;

		public override Task Given() {
			return CreateContinuousProjection(Guid.NewGuid().ToString());
		}

		public override async Task When() {
			_result = await _projManager.ListAllAsync(_credentials);
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

		public override Task Given() {
			return CreateOneTimeProjection();
		}

		public override async Task When() {
			_result = (await _projManager.ListOneTimeAsync(_credentials)).ToList();
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

		public override Task Given() {
			_projectionName = Guid.NewGuid().ToString();
			return CreateContinuousProjection(_projectionName);
		}

		public override async Task When() {
			_result = (await _projManager.ListContinuousAsync(_credentials)).ToList();
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

		public override async Task Given() {
			_projectionName = "when_getting_projection_information";
			_streamName = "test-stream-" + Guid.NewGuid().ToString();

			await PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			await PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");
		}

		public override Task When() {
			_query = CreateStandardQuery(_streamName);
			return _projManager.CreateContinuousAsync(_projectionName, _query, _credentials);
		}

		[Test]
		public async Task should_be_able_to_get_the_projection_state() {
			var state = await _projManager.GetStateAsync(_projectionName, _credentials);
			Assert.IsNotEmpty(state);
		}

		[Test]
		public async Task should_be_able_to_get_the_projection_status() {
			var status = await _projManager.GetStatusAsync(_projectionName, _credentials);
			Assert.IsNotEmpty(status);
		}

		[Test]
		public async Task should_be_able_to_get_the_projection_result() {
			var result = await _projManager.GetResultAsync(_projectionName, _credentials);
			Assert.AreEqual("{\"count\":1}", result);
		}

		[Test]
		public async Task should_be_able_to_get_the_projection_query() {
			var query = await _projManager.GetQueryAsync(_projectionName, _credentials);
			Assert.AreEqual(_query, query);
		}
	}

	[TestFixture]
	[Category("ProjectionsManager")]
	public class when_updating_a_projection_query : SpecificationWithNodeAndProjectionsManager {
		private string _projectionName;
		private string _streamName;
		private string _newQuery;

		public override async Task Given() {
			_projectionName = "when_updating_a_projection_query";
			_streamName = "test-stream-" + Guid.NewGuid().ToString();

			await PostEvent(_streamName, "testEvent", "{\"A\":\"1\"}");
			await PostEvent(_streamName, "testEvent", "{\"A\":\"2\"}");

			var origQuery = CreateStandardQuery(_streamName);
			_newQuery = CreateStandardQuery("DifferentStream");
			await _projManager.CreateContinuousAsync(_projectionName, origQuery, _credentials);
		}

		public override Task When() {
			return _projManager.UpdateQueryAsync(_projectionName, _newQuery, _credentials);
		}

		[Test]
		public async Task should_update_the_projection_query() {
			var query = await _projManager.GetQueryAsync(_projectionName, _credentials);
			Assert.AreEqual(_newQuery, query);
		}
	}
}
