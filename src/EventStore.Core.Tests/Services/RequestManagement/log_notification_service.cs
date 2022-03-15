using System;
using System.Threading.Tasks;
using EventStore.Core.Services.RequestManager;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RequestManagement {
	public class log_notification_service {
		private LogNotificationService _sut;

		[SetUp]
		public void SetUp() => _sut = new LogNotificationService("test");

		[Test]
		public async Task is_notified_when_log_position_is_updated_to_requested_position() {
			var task = _sut.WaitFor(2);
			_sut.UpdateLogPosition(2);
			await task.WithTimeout();
			Assert.True(task.IsCompleted);
		}

		[Test]
		public async Task is_notified_when_log_position_is_updated_to_requested_position_plus_one() {
			var task = _sut.WaitFor(2);
			_sut.UpdateLogPosition(3);
			await task.WithTimeout();
			Assert.True(task.IsCompleted);
		}

		[Test]
		public void is_not_notified_when_log_position_is_updated_to_requested_position_minus_one() {
			var task = _sut.WaitFor(2);
			_sut.UpdateLogPosition(1);
			Assert.ThrowsAsync<TimeoutException>(async () => await task.WithTimeout(500));
		}

		[Test]
		public async Task is_notified_when_log_position_is_updated_prior_to_waiting() {
			_sut.UpdateLogPosition(3);
			var task = _sut.WaitFor(2);
			await task.WithTimeout();
			Assert.True(task.IsCompleted);
		}

		[Test]
		public async Task is_notified_when_waiting_on_log_position_zero_without_log_update() {
			var task = _sut.WaitFor(0);
			await task.WithTimeout();
			Assert.True(task.IsCompleted);
		}

		[Test]
		public async Task is_notified_when_there_are_multiple_waiters_on_same_position() {
			var task1 = _sut.WaitFor(2);
			var task2 = _sut.WaitFor(2);
			_sut.UpdateLogPosition(2);
			await Task.WhenAll(task1, task2).WithTimeout();
			Assert.True(task1.IsCompleted);
			Assert.True(task2.IsCompleted);
		}

		[Test]
		public async Task is_notified_correctly_when_there_are_multiple_waiters() {
			var tasks = new Task[10];
			for (int i = 0; i < 10; i++)
				tasks[i] = _sut.WaitFor(i);

			_sut.UpdateLogPosition(4);
			await Task.Delay(500);

			for (int i = 0; i < 10; i++) {
				if (i < 5)
					Assert.True(tasks[i].IsCompleted, $"Task {i}");
				else
					Assert.False(tasks[i].IsCompleted, $"Task {i}");
			}
		}

		[TearDown]
		public void TearDown() => _sut?.Dispose();
	}
}
