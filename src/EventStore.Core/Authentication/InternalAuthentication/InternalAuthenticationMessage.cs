using EventStore.Core.Messaging;

namespace EventStore.Core.Authentication.InternalAuthentication
{
	public static class InternalAuthenticationMessage {
		public class AuthenticationProviderInitialized : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}
	}
}
