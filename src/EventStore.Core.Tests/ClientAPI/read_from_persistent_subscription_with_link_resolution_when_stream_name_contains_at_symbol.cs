extern alias GrpcClient;
extern alias GrpcClientPersistent;
using System;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;
using StreamPosition = GrpcClient::EventStore.Client.StreamPosition;
using PersistentSubscriptionSettings = GrpcClientPersistent::EventStore.Client.PersistentSubscriptionSettings;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("LongRunning"), Category("ClientAPI")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_from_persistent_subscription_with_link_resolution_when_stream_name_contains_at_symbol<TLogFormat, TStreamId> : SpecificationWithMiniNode<TLogFormat, TStreamId> {
		private string _result;

		protected override async Task When() {
			var task = new TaskCompletionSource<string>();

			var setts = new PersistentSubscriptionSettings(resolveLinkTos: false, startFrom: StreamPosition.Start);

			await _conn.CreatePersistentSubscriptionAsync("link", "Agroup", setts, DefaultData.AdminCredentials);
			await _conn.ConnectToPersistentSubscriptionAsync(
				"link",
				"Agroup",
				(sub, @event) => {
					var data = Encoding.Default.GetString(@event.Event.Data.ToArray());
					task.TrySetResult(data);
					return Task.CompletedTask;
				},
				(sub, reason, ex) => { }, DefaultData.AdminCredentials);

			await _conn.AppendToStreamAsync("target@me@com", ExpectedVersion.NoStream, TestEvent.NewTestEvent("data", eventName: "AEvent"));
			await _conn.AppendToStreamAsync("link", ExpectedVersion.NoStream, TestEvent.NewTestEvent("0@target@me@com", eventName: "$>"));

			_result = await Task.WhenAny(task.Task, Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => "timeout")).Result;
		}

		[Test]
		public void the_subscription_resolve_the_link_properly() {
			Assert.AreEqual(_result, "data");
		}
	}
}
