using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Tests.Bus.Helpers;

public class FakeCollectingQueuedHandler : IQueuedHandler {
	public List<Message> PublishedMessages { get; } = [];

	public void Handle(Message message) { }

	public void Publish(Message message) {
		PublishedMessages.Add(message);
	}

	public string Name => string.Empty;
	public Task Start() => Task.CompletedTask;

	public void Stop() {}

	public void RequestStop() { }

	public QueueStats GetStatistics() => null;
}
