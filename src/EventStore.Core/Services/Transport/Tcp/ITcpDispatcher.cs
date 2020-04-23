using System.Security.Claims;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.Transport.Tcp {
	public interface ITcpDispatcher {
		TcpPackage? WrapMessage(Message message, byte version);

		Message UnwrapPackage(TcpPackage package, IEnvelope envelope, ClaimsPrincipal user, string login, string pass,
			TcpConnectionManager connection, byte version);
	}
}
