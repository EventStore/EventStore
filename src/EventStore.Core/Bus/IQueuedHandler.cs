using EventStore.Core.Messaging;

namespace EventStore.Core.Bus
{
    public interface IQueuedHandler: IHandle<Message>, IPublisher
    {
        void Start();
        void Stop();
        void RequestStop();
    }
}