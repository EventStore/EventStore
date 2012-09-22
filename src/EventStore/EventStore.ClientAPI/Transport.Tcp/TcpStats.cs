using System;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Transport.Tcp
{
    class TcpStats
    {
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

        public readonly string SentBytesTotalFriendly;
        public readonly string ReceivedBytesTotalFriendly;
        public readonly string SendingSpeedFriendly;
        public readonly string ReceivingSpeedFriendly;
        public readonly string MeasureTimeFriendly;

        public TcpStats(int connections,
                        long sentBytesTotal,
                        long receivedBytesTotal,
                        long sentBytesSinceLastRunSinceLastRun,
                        long receivedBytesSinceLastRun,
                        long pendingSend,
                        long inSend,
                        long pendingReceived,
                        TimeSpan measureTime)
        {
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
            ReceivingSpeed = (MeasureTime.TotalSeconds < 0.00001) ? 0 : ReceivedBytesSinceLastRun / MeasureTime.TotalSeconds;

            SentBytesTotalFriendly = SentBytesTotal.ToFriendlySizeString();
            ReceivedBytesTotalFriendly = ReceivedBytesTotal.ToFriendlySizeString();
            SendingSpeedFriendly = SendingSpeed.ToFriendlySpeedString();
            ReceivingSpeedFriendly = ReceivingSpeed.ToFriendlySpeedString();
            MeasureTimeFriendly = string.Format(@"{0:s\.fff}s", MeasureTime);
        }
    }
}