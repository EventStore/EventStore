using System;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI.SystemData {
	internal class StatusCode {
		public static SliceReadStatus Convert(ClientMessage.ReadStreamEventsCompleted.ReadStreamResult code) {
			switch (code) {
				case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.Success:
					return SliceReadStatus.Success;
				case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.NoStream:
					return SliceReadStatus.StreamNotFound;
				case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.StreamDeleted:
					return SliceReadStatus.StreamDeleted;
				default:
					throw new ArgumentOutOfRangeException("code");
			}
		}
	}
}
