using System;
using System.Linq;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI {
	public class list_projections : specification_with_standard_projections_runnning {
		const string TestProjection =
			"fromAll().when({$init: function (state, ev) {return {};},ConversationStarted: function (state, ev) {state.lastBatchSent = ev;return state;}});";

		[Test]
		public void list_all_projections_works() {
			var x = _manager.ListAllAsync(new UserCredentials("admin", "changeit")).Result;
			Assert.AreEqual(true, x.Any());
			Assert.IsTrue(x.Any(p => p.Name == "$streams"));
		}

		[Test]
		public void list_oneTime_projections_works() {
			_manager.CreateOneTimeAsync(TestProjection, new UserCredentials("admin", "changeit")).Wait();
			var x = _manager.ListOneTimeAsync(new UserCredentials("admin", "changeit")).Result;
			Assert.AreEqual(true, x.Any(p => p.Mode == "OneTime"));
		}

		[Test]
		public void list_continuous_projections_works() {
			var nameToTest = Guid.NewGuid().ToString();
			_manager.CreateContinuousAsync(nameToTest, TestProjection, new UserCredentials("admin", "changeit")).Wait();
			var x = _manager.ListContinuousAsync(new UserCredentials("admin", "changeit")).Result;
			Assert.AreEqual(true, x.Any(p => p.Name == nameToTest));
		}
	}
}
