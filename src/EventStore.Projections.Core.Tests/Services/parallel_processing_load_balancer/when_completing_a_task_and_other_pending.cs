using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer {
	[TestFixture]
	public class when_completing_a_task_and_other_pending : specification_with_parallel_processing_load_balancer {
		private bool _task5Scheduled;

		protected override void Given() {
			_task5Scheduled = false;

			_balancer.ScheduleTask("task1", (s, i) => { });
			_balancer.ScheduleTask("task2", (s, i) => { });
			_balancer.ScheduleTask("task3", (s, i) => { });
			_balancer.ScheduleTask("task4", (s, i) => { });
			_balancer.ScheduleTask("task5", (task, worker) => _task5Scheduled = true);
			Assume.That(!_task5Scheduled);
		}

		protected override void When() {
			_balancer.AccountCompleted("task3");
		}


		[Test]
		public void last_task_becomes_scheduled() {
			Assert.IsTrue(_task5Scheduled);
		}
	}
}
