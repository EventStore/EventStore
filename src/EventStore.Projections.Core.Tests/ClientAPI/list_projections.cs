using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Tests;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class list_projections<TLogFormat, TStreamId> : specification_with_standard_projections_runnning<TLogFormat, TStreamId> {
		const string TestProjection =
			"fromAll().when({$init: function (state, ev) {return {};},ConversationStarted: function (state, ev) {state.lastBatchSent = ev;return state;}});";

		[Test]
		public async Task list_all_projections_works() {
			var x = _manager.ListAllAsync(userCredentials: DefaultData.AdminCredentials);
			Assert.AreEqual(true, await x.AnyAsync());
			Assert.IsTrue(await x.AnyAsync(p => p.Name == "$streams"));
		}

		[Test]
		public async Task list_oneTime_projections_works() {
			await _manager.CreateOneTimeAsync(TestProjection, userCredentials: DefaultData.AdminCredentials);
			var x = _manager.ListOneTimeAsync(userCredentials: DefaultData.AdminCredentials);
			Assert.AreEqual(true, await x.AnyAsync(p => p.Mode == "OneTime"));
		}

		[Test]
		public async Task list_continuous_projections_works() {
			var nameToTest = Guid.NewGuid().ToString();
			await _manager.CreateContinuousAsync(nameToTest, TestProjection, userCredentials: DefaultData.AdminCredentials);
			var x = _manager.ListContinuousAsync(userCredentials: DefaultData.AdminCredentials);
			Assert.AreEqual(true, await x.AnyAsync(p => p.Name == nameToTest));
		}
	}
}
