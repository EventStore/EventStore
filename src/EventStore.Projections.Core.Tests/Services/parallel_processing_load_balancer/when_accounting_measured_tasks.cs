using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer {
	[TestFixture]
	public class when_accounting_measured_tasks : specification_with_parallel_processing_load_balancer {
		private int _task2ScheduledOn;

		protected override void Given() {
			_balancer.ScheduleTask("task1", OnScheduled);
			_balancer.ScheduleTask("task2", OnScheduled);
		}

		protected override void When() {
			_balancer.AccountMeasured("task1", 1000);
			_balancer.AccountMeasured("task2", 100);
		}

		private void OnScheduled(string task, int worker) {
			switch (task) {
				case "task1": {
					break;
				}
				case "task2": {
					_task2ScheduledOn = worker;
					break;
				}
				default:
					Assert.Inconclusive();
					break;
			}
		}

		[Test]
		public void schedules_on_least_loaded_worker() {
			var scheduledOn = -1;
			_balancer.ScheduleTask("task3", (s, on) => { scheduledOn = on; });

			Assert.AreEqual(_task2ScheduledOn, scheduledOn);
		}

		[Test]
		public void schedules_nearest_tasks_on_least_loaded_worker() {
			var scheduled3On = -1;
			var scheduled4On = -1;
			_balancer.ScheduleTask("task3", (s, on) => { scheduled3On = on; });
			_balancer.ScheduleTask("task4", (s, on) => { scheduled4On = on; });

			Assert.AreEqual(_task2ScheduledOn, scheduled3On);
			Assert.AreEqual(_task2ScheduledOn, scheduled4On);
		}
	}
}
