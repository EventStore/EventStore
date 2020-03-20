using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Authentication {
	public interface IAuthenticationProviderPublisher {
		void Publish(AuthenticationProviderMessage message);
	}

	public abstract class AuthenticationProviderMessage {
	}

	public class AuthenticationMessage {
		public sealed class AuthenticationProviderInitialized : AuthenticationProviderMessage {
		}
	}

	public class InternalAuthenticationMessage {
		public class AuthenticationProviderInitialized : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
			
			public override int MsgTypeId {
				get { return TypeId; }
			}
		}
	}

	public class ShimmedPublisher : IAuthenticationProviderPublisher {
		private readonly IPublisher _mainQueue;
		public ShimmedPublisher(IPublisher mainQueue) {
			_mainQueue = mainQueue;
		}
		
		public void Publish(AuthenticationProviderMessage message) {
			if (message is AuthenticationMessage.AuthenticationProviderInitialized) {
				_mainQueue.Publish(new InternalAuthenticationMessage.AuthenticationProviderInitialized());
			}
		}
	}
}
