using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Bus
{
    public interface IQueuedHandler: IHandle<Message>, IPublisher
    {
        string Name { get; }
        void Start();
        void Stop();
        void RequestStop();
        //void Publish(Message message);
        QueueStats GetStatistics();
    }
}