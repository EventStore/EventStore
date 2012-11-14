using EventStore.Core.Messages;

namespace EventStore.TestClient
{
    internal static class ClientEventUtil
    {
        public static TcpClientMessageDto.ClientEvent FromDataEvent(Core.Data.Event evnt)
        {
            return new TcpClientMessageDto.ClientEvent(evnt.EventId.ToByteArray(), evnt.EventType, evnt.IsJson, evnt.Data,
                                                       evnt.Metadata);
        }
    }
}
