using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Tests.Bus;

public class FakeQueuedHandler : IQueuedHandler {
	public string Name => string.Empty;
	public void Handle(Message message) { }
	public void Publish(Message message) { }
	public Task Start() => Task.CompletedTask;
	public void Stop() { }
	public void RequestStop() { }
	public QueueStats GetStatistics() => null;
}
