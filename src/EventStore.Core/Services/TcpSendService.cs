using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Services {
	public class TcpSendService : IHandle<TcpMessage.TcpSend> {
		public void Handle(TcpMessage.TcpSend message) {
			// todo: histogram metric?
			message.ConnectionManager.SendMessage(message.Message);
		}
	}
}
