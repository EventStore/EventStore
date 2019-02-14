using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer {
	[TestFixture]
	public class when_scheduling_first_task : specification_with_parallel_processing_load_balancer {
		private string _scheduledTask;
		private int _scheduledOnWorker;

		protected override void Given() {
			_scheduledTask = null;
			_scheduledOnWorker = int.MinValue;
		}

		protected override void When() {
			_balancer.ScheduleTask(
				"task1", (task, worker) => {
					_scheduledTask = task;
					_scheduledOnWorker = worker;
				});
		}

		[Test]
		public void schedules_correct_task() {
			Assert.AreEqual("task1", _scheduledTask);
		}

		[Test]
		public void schedules_on_any_worker() {
			Assert.AreNotEqual(int.MinValue, _scheduledOnWorker);
		}
	}
}
