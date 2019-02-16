using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer {
	[TestFixture]
	public class when_scheduling_many_unmeasured_tasks : specification_with_parallel_processing_load_balancer {
		private bool _task5Scheduled;

		protected override void Given() {
			_task5Scheduled = false;

			_balancer.ScheduleTask("task1", (s, i) => { });
			_balancer.ScheduleTask("task2", (s, i) => { });
			_balancer.ScheduleTask("task3", (s, i) => { });
			_balancer.ScheduleTask("task4", (s, i) => { });
		}

		protected override void When() {
			_balancer.ScheduleTask("task5", (task, worker) => _task5Scheduled = true);
		}


		[Test]
		public void last_task_remains_unscheduled() {
			Assert.IsFalse(_task5Scheduled);
		}
	}
}
