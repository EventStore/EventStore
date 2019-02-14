using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer {
	[TestFixture]
	public class when_scheduling_first_tasks : specification_with_parallel_processing_load_balancer {
		private List<string> _scheduledTasks;
		private List<int> _scheduledOnWorkers;
		private int _scheduled;

		protected override void Given() {
			_scheduled = 0;
			_scheduledTasks = new List<string>();
			_scheduledOnWorkers = new List<int>();
		}

		protected override void When() {
			_balancer.ScheduleTask("task1", OnScheduled);
			_balancer.ScheduleTask("task2", OnScheduled);
		}

		private void OnScheduled(string task, int worker) {
			_scheduled++;
			_scheduledTasks.Add(task);
			_scheduledOnWorkers.Add(worker);
		}

		[Test]
		public void schedules_all_tasks() {
			Assert.AreEqual(2, _scheduled);
		}

		[Test]
		public void schedules_correct_tasks() {
			Assert.That(new[] {"task1", "task2"}.SequenceEqual(_scheduledTasks));
		}

		[Test]
		public void schedules_on_different_workers() {
			Assert.That(_scheduledOnWorkers.Distinct().Count() == 2);
		}
	}
}
