using System;

namespace EventStore.Transport.Tcp {
	public class TcpStats {
		public readonly int Connections;
		public readonly long SentBytesTotal;
		public readonly long ReceivedBytesTotal;
		public readonly long SentBytesSinceLastRun;
		public readonly long ReceivedBytesSinceLastRun;
		public readonly double SendingSpeed;
		public readonly double ReceivingSpeed;
		public readonly long PendingSend;
		public readonly long InSend;
		public readonly long PendingReceived;
		public readonly TimeSpan MeasureTime;

		public TcpStats(int connections,
			long sentBytesTotal,
			long receivedBytesTotal,
			long sentBytesSinceLastRunSinceLastRun,
			long receivedBytesSinceLastRun,
			long pendingSend,
			long inSend,
			long pendingReceived,
			TimeSpan measureTime) {
			Connections = connections;
			SentBytesTotal = sentBytesTotal;
			ReceivedBytesTotal = receivedBytesTotal;
			SentBytesSinceLastRun = sentBytesSinceLastRunSinceLastRun;
			ReceivedBytesSinceLastRun = receivedBytesSinceLastRun;
			PendingSend = pendingSend;
			InSend = inSend;
			PendingReceived = pendingReceived;
			MeasureTime = measureTime;
			SendingSpeed = (MeasureTime.TotalSeconds < 0.00001) ? 0 : SentBytesSinceLastRun / MeasureTime.TotalSeconds;
			ReceivingSpeed = (MeasureTime.TotalSeconds < 0.00001)
				? 0
				: ReceivedBytesSinceLastRun / MeasureTime.TotalSeconds;
		}
	}
}
