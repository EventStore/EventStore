using EventStore.Core.Messaging;

namespace EventStore.ClientAPI.Embedded
{
    internal interface IEmbeddedResponse
    {
        void InspectMessage(Message message);
    }
    internal class EmbeddedResponseEnvelope : IEnvelope
    {
        private readonly IEmbeddedResponse _response;

        public EmbeddedResponseEnvelope(IEmbeddedResponse response)
        {
            _response = response;
        }

        public void ReplyWith<T>(T message) where T : Message
        {
            _response.InspectMessage(message);
        }
    }
}