using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer {
	[TestFixture]
	public class when_scheduling_many_measured_tasks : specification_with_parallel_processing_load_balancer {
		private bool _task3Scheduled;

		protected override void Given() {
			_task3Scheduled = false;

			_balancer.ScheduleTask("task1", (s, i) => { });
			_balancer.ScheduleTask("task2", (s, i) => { });
			_balancer.AccountMeasured("task1", 1000);
			_balancer.AccountMeasured("task2", 1000);
		}

		protected override void When() {
			_balancer.ScheduleTask("task3", (task, worker) => _task3Scheduled = true);
		}


		[Test]
		public void last_task_remains_unscheduled() {
			Assert.IsFalse(_task3Scheduled);
		}
	}
}
