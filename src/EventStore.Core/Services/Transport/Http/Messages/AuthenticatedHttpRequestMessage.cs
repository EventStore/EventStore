using System;
using EventStore.Core.Messaging;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Messages {
	class AuthenticatedHttpRequestMessage : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		public readonly HttpEntityManager Manager;
		public readonly UriToActionMatch Match;

		public AuthenticatedHttpRequestMessage(HttpEntityManager manager, UriToActionMatch match) {
			Manager = manager;
			Match = match;
		}
	}
}
