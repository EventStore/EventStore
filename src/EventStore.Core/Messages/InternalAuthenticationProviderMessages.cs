using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public abstract class InternalAuthenticationProviderMessages {
		public sealed class ResetPasswordCache : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string LoginName;

			public ResetPasswordCache(string loginName) {
				LoginName = loginName;
			}
		}
	}
}
