// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  
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