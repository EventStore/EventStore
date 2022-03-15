using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.RequestManager;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RequestManagement {
	
		[TestFixture]
	public class log_notification_tracker_perf {
		private void Increment( ref long counter) {			
			Interlocked.Increment(ref counter);
		}
		[Test]
		public void can_notify() {
			LogNotificationService _tracker = new LogNotificationService("test");
			long _notified = 0;
			_tracker.Waitfor(1000).ContinueWith((_) => Increment(ref _notified));
			
			_tracker.UpdateLogPosition(1000);
			AssertEx.IsOrBecomesTrue(() => _notified == 1);
		}

		[Test]
		public void can_notify_10_000() {
			LogNotificationService _tracker = new LogNotificationService("test");
			long _notified = 0;
			var count = 10_000;
			for (int i = 0; i < count; i++) {
				_tracker.Waitfor(i).ContinueWith((_) => Increment(ref _notified));
			}
			var stopwatch = Stopwatch.StartNew();
			_tracker.UpdateLogPosition(count);

			AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _notified) == count, System.TimeSpan.FromSeconds(10), $"Timed out notified:{Interlocked.Read(ref _notified)}");
			stopwatch.Stop();
			TestContext.Out.WriteLine($"{count} notifications in {stopwatch.ElapsedMilliseconds} ms. n/ms = {count / stopwatch.ElapsedMilliseconds} ");
		}
		
		[Test]
		public void can_notify_1_000_000_batched() {
			LogNotificationService _tracker = new LogNotificationService("test");
			long _notified = 0;
			var count = 10_000;
			var pos = 0;
			var stopwatch = Stopwatch.StartNew();
			for (int n = 0; n < 100; n++) {
				for (int i = 0; i < count; i++) {
					pos++;
					_tracker.Waitfor(pos).ContinueWith((_) => Increment(ref _notified));
				}
				stopwatch.Restart();
				_tracker.UpdateLogPosition(pos);

				AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _notified) == pos, System.TimeSpan.FromSeconds(30), $"Timed out notified:{Interlocked.Read(ref _notified)}");
				stopwatch.Stop();
				TestContext.Out.WriteLine($"{count} notifications in {stopwatch.ElapsedMilliseconds} ms. n/ms = {count / Math.Max(stopwatch.ElapsedMilliseconds, 1)} ");
			}
		}
		[Test]
		public void can_notify_1_000_000() {
			LogNotificationService _tracker = new LogNotificationService("Test");
			long _notified = 0;
			var count = 1_000_000; 
			var pos = 0;
			var stopwatch = Stopwatch.StartNew();

			for (int i = 0; i < count; i++) {
				pos++;
				_tracker.Waitfor(pos).ContinueWith((_) => Increment(ref _notified));
			}
			stopwatch.Restart();

			_tracker.UpdateLogPosition(10_000);
			AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _notified) == 10_000, System.TimeSpan.FromSeconds(5), $"Timed out waiting for 10,000 notified:{Interlocked.Read(ref _notified)}");
			_tracker.UpdateLogPosition(20_000);
			AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _notified) == 20_000, System.TimeSpan.FromSeconds(5), $"Timed out waiting for 20,000 notified:{Interlocked.Read(ref _notified)}");
			_tracker.UpdateLogPosition(300_000);
			AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _notified) == 300_000, System.TimeSpan.FromSeconds(5), $"Timed out waiting for 300,000 notified:{Interlocked.Read(ref _notified)}");
			_tracker.UpdateLogPosition(500_000);
			AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _notified) == 500_000, System.TimeSpan.FromSeconds(5), $"Timed out waiting for 500,000 notified:{Interlocked.Read(ref _notified)}");
			_tracker.UpdateLogPosition(pos);
			AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _notified) == pos, System.TimeSpan.FromSeconds(10), $"Timed out waiting for {pos} notified:{Interlocked.Read(ref _notified)}");
			stopwatch.Stop();
			TestContext.Out.WriteLine($"{count} notifications in {stopwatch.ElapsedMilliseconds} ms. n/ms = {count / Math.Max(stopwatch.ElapsedMilliseconds, 1)} ");

		}

	}
}
