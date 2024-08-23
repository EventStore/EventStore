using System.Threading.Tasks;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Bus;

public interface IQueuedHandler : IPublisher {
	string Name { get; }
	Task Start();
	void Stop();

	void RequestStop();
	
	QueueStats GetStatistics();
}
