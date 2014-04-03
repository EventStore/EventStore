using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core
{
    public interface ISubsystem
    {
        void Register(
            TFChunkDb db,
            QueuedHandler mainQueue,
            ISubscriber mainBus,
            TimerService timerService,
            ITimeProvider timeProvider,
            IHttpForwarder httpForwarder,
            HttpService[] httpServices,
            IPublisher networkSendService);

        void Start();
        void Stop();
    }
}
