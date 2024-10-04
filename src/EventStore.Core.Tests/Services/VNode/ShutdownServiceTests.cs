using System;
using System.Net;
using DotNext.Net.Http;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.VNode;

public class ShutdownServiceTests {
	[Test]
	public void should_graceful_shutdown() {
		var queue = new FakeCollectingQueuedHandler();
		var nodeInfo = new VNodeInfo(
			Guid.NewGuid(),
			0,
			new IPEndPoint(0, 0),
			new IPEndPoint(IPAddress.Loopback, 1),
			new IPEndPoint(IPAddress.Loopback, 2),
			new IPEndPoint(IPAddress.Loopback, 3),
			new HttpEndPoint(new Uri("http://www.eventstore.com")), true);

		var sut = new ShutdownService(queue, nodeInfo);
		var terminated = false;

		sut.Handle(new SystemMessage.RegisterForGracefulTermination("foo", () => terminated = true));
		sut.Handle(new ClientMessage.RequestShutdown(true, true));
		sut.Handle(new SystemMessage.ComponentTerminated("foo"));

		Assert.True(terminated);
		Assert.IsInstanceOf<SystemMessage.BecomeShuttingDown>(queue.PublishedMessages[0]);
	}
}
