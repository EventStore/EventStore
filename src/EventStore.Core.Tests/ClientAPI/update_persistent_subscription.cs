using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class update_existing_persistent_subscription : SpecificationWithMiniNode {
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent();

		protected override void Given() {
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
				new EventData(Guid.NewGuid(), "whatever", true, Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
			_conn.CreatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials)
				.Wait();
		}

		protected override void When() {
		}

		[Test]
		public void the_completion_succeeds() {
			Assert.DoesNotThrow(() =>
				_conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials)
					.Wait());
		}
	}

	[TestFixture, Category("LongRunning")]
	public class update_existing_persistent_subscription_with_subscribers : SpecificationWithMiniNode {
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent();

		private readonly AutoResetEvent _dropped = new AutoResetEvent(false);
		private SubscriptionDropReason _reason;
		private Exception _exception;
		private Exception _caught = null;

		protected override void Given() {
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
				new EventData(Guid.NewGuid(), "whatever", true, Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
			_conn.CreatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials)
				.Wait();
			_conn.ConnectToPersistentSubscription(_stream, "existing", (x, y) => Task.CompletedTask,
				(sub, reason, ex) => {
					_dropped.Set();
					_reason = reason;
					_exception = ex;
				});
		}

		protected override void When() {
			try {
				_conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials)
					.Wait();
			} catch (Exception ex) {
				_caught = ex;
			}
		}

		[Test]
		public void the_completion_succeeds() {
			Assert.IsNull(_caught);
		}

		[Test]
		public void existing_subscriptions_are_dropped() {
			Assert.IsTrue(_dropped.WaitOne(TimeSpan.FromSeconds(5)));
			Assert.AreEqual(SubscriptionDropReason.UserInitiated, _reason);
			Assert.IsNull(_exception);
		}
	}


	[TestFixture, Category("LongRunning")]
	public class update_non_existing_persistent_subscription : SpecificationWithMiniNode {
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent();

		protected override void When() {
		}

		[Test]
		public void the_completion_fails_with_not_found() {
			try {
				_conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings,
					DefaultData.AdminCredentials).Wait();
				Assert.Fail("should have thrown");
			} catch (Exception ex) {
				Assert.IsInstanceOf<AggregateException>(ex);
				Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException);
			}
		}
	}

	[TestFixture, Category("LongRunning")]
	public class update_existing_persistent_subscription_without_permissions : SpecificationWithMiniNode {
		private readonly string _stream = Guid.NewGuid().ToString();

		private readonly PersistentSubscriptionSettings _settings = PersistentSubscriptionSettings.Create()
			.DoNotResolveLinkTos()
			.StartFromCurrent();

		protected override void When() {
			_conn.AppendToStreamAsync(_stream, ExpectedVersion.Any,
				new EventData(Guid.NewGuid(), "whatever", true, Encoding.UTF8.GetBytes("{'foo' : 2}"), new Byte[0]));
			_conn.CreatePersistentSubscriptionAsync(_stream, "existing", _settings, DefaultData.AdminCredentials)
				.Wait();
		}

		[Test]
		public void the_completion_fails_with_access_denied() {
			try {
				_conn.UpdatePersistentSubscriptionAsync(_stream, "existing", _settings, null).Wait();
				Assert.Fail("should have thrown");
			} catch (Exception ex) {
				Assert.IsInstanceOf<AggregateException>(ex);
				Assert.IsInstanceOf<AccessDeniedException>(ex.InnerException);
			}
		}
	}
}
