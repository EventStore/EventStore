using System;

namespace EventStore.ClientAPI.Transport.Tcp
{
    internal class TcpStats
    {
        ///<summary>
        ///Number of TCP connections to Event Store
        ///</summary>
        public readonly int Connections;
        ///<summary>
        ///Total bytes sent from TCP connections
        ///</summary>
        public readonly long SentBytesTotal;
        ///<summary>
        ///Total bytes sent to TCP connections
        ///</summary>
        public readonly long ReceivedBytesTotal;
        ///<summary>
        ///TBD
        ///</summary>
        public readonly long SentBytesSinceLastRun;
        ///<summary>
        ///TBD
        ///</summary>
        public readonly long ReceivedBytesSinceLastRun;
        ///<summary>
        ///TBD
        ///</summary>
        public readonly double SendingSpeed;
        ///<summary>
        ///TBD
        ///</summary>
        public readonly double ReceivingSpeed;
        ///<summary>
        ///TBD
        ///</summary>
        public readonly long PendingSend;
        ///<summary>
        ///TBD
        ///</summary>
        public readonly long InSend;
        ///<summary>
        ///TBD
        ///</summary>
        public readonly long PendingReceived;
        ///<summary>
        ///TBD
        ///</summary>
        public readonly TimeSpan MeasureTime;

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
            SendingSpeed = MeasureTime.TotalSeconds < 0.00001 ? 0 : SentBytesSinceLastRun / MeasureTime.TotalSeconds;
            ReceivingSpeed = MeasureTime.TotalSeconds < 0.00001 ? 0 : ReceivedBytesSinceLastRun / MeasureTime.TotalSeconds;
        }
    }
}