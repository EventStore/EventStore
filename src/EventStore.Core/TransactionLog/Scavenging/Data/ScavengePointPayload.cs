using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Scavenging {
	// These are stored in the data of the payload record
	public class ScavengePointPayload {
		public int Threshold { get; set; }

		public byte[] ToJsonBytes() =>
			Json.ToJsonBytes(this);

		public static ScavengePointPayload FromBytes(byte[] bytes) =>
			Json.ParseJson<ScavengePointPayload>(bytes);
	}
}
