using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests {
	[TestFixture]
	public class ExclusiveDbLockTests {
		[Test]
		public async Task can_release_when_running_in_task_pool() {
			var dbPath = $"/tmp/eventstore/{Guid.NewGuid()}";
			using var sut = new ExclusiveDbLock(dbPath);
			Assert.True(sut.Acquire());
			Assert.True(sut.IsAcquired);
			await Task.Delay(1);
			sut.Release();
		}
	}
}
