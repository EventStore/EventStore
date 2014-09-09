using System;
using EventStore.Core.Bus;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class UUIDController : CommunicationController
    {
        public UUIDController(IPublisher publisher)
            : base(publisher)
        {
        }

        protected override void SubscribeCore(IHttpService service)
        {
            service.RegisterAction(
                new ControllerAction("/new-uuid", "GET", Codec.NoCodecs, new ICodec[] {Codec.Text}),
                (manager, match) => manager.Reply(Guid.NewGuid().ToString("D"), 200, "OK", "text/plain"));
        }
    }
}