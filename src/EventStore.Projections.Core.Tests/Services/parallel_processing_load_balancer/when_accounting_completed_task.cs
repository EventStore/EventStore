using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer {
	[TestFixture]
	public class when_accounting_completed_task : specification_with_parallel_processing_load_balancer {
		private int _task1ScheduledOn;

		protected override void Given() {
			_balancer.ScheduleTask("task1", OnScheduled);
			_balancer.ScheduleTask("task2", OnScheduled);
			_balancer.AccountMeasured("task1", 1000);
			_balancer.AccountMeasured("task2", 100);
		}

		protected override void When() {
			_balancer.AccountCompleted("task1");
		}

		private void OnScheduled(string task, int worker) {
			switch (task) {
				case "task1": {
					_task1ScheduledOn = worker;
					break;
				}
				case "task2": {
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
			_balancer.ScheduleTask("task3", (s, on) => { scheduledOn = @on; });

			Assert.AreEqual(_task1ScheduledOn, scheduledOn);
		}
	}
}
