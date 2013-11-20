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
using System.Collections.Concurrent;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Transport.Tcp
{
    public class TcpConnectionMonitor
    {
        public static readonly TcpConnectionMonitor Default = new TcpConnectionMonitor();
        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpConnectionMonitor>();

        private readonly object _statsLock = new object();

        private readonly ConcurrentDictionary<IMonitoredTcpConnection, ConnectionData> _connections = new ConcurrentDictionary<IMonitoredTcpConnection, ConnectionData>();

        private long _sentTotal;
        private long _receivedTotal;
        private long _sentSinceLastRun;
        private long _receivedSinceLastRun;
        private long _pendingSendOnLastRun;
        private long _inSendOnLastRun;
        private long _pendingReceivedOnLastRun;

        private bool _anySendBlockedOnLastRun;
        private DateTime _lastUpdateTime;

        private TcpConnectionMonitor()
        {
        }

        public void Register(IMonitoredTcpConnection connection)
        {
            _connections.TryAdd(connection, new ConnectionData(connection));
        }

        public void Unregister(IMonitoredTcpConnection connection)
        {
            ConnectionData data;
            _connections.TryRemove(connection, out data);
        }

        public TcpStats GetTcpStats()
        {
            ConnectionData[] connections = _connections.Values.ToArray();
            lock (_statsLock)
            {
                var stats = AnalyzeConnections(connections, DateTime.UtcNow - _lastUpdateTime);
                _lastUpdateTime = DateTime.UtcNow;
                return stats;
            }
        }

        private TcpStats AnalyzeConnections(ConnectionData[] connections, TimeSpan measurePeriod)
        {
            _receivedSinceLastRun = 0;
            _sentSinceLastRun = 0;
            _pendingSendOnLastRun = 0;
            _inSendOnLastRun = 0;
            _pendingReceivedOnLastRun = 0;
            _anySendBlockedOnLastRun = false;

            foreach (var connection in connections)
            {
                AnalyzeConnection(connection);
            }

            var stats = new TcpStats(connections.Length,
                                     _sentTotal,
                                     _receivedTotal,
                                     _sentSinceLastRun,
                                     _receivedSinceLastRun,
                                     _pendingSendOnLastRun,
                                     _inSendOnLastRun,
                                     _pendingReceivedOnLastRun,
                                     measurePeriod);


            if (Application.IsDefined(Application.DumpStatistics))
            {
                Log.Trace("\n# Total connections: {0,3}. Out: {1:0.00}b/s  In: {2:0.00}b/s  Pending Send: {3}  " +
                          "In Send: {4}  Pending Received: {5} Measure Time: {6}",
                          stats.Connections,
                          stats.SendingSpeed,
                          stats.ReceivingSpeed,
                          stats.PendingSend,
                          stats.InSend,
                          stats.PendingSend,
                          stats.MeasureTime);
            }
            return stats;
        }

        private void AnalyzeConnection(ConnectionData connectionData)
        {
            var connection = connectionData.Connection;
            if (!connection.IsInitialized)
                return;

            if (connection.IsFaulted)
            {
                Log.Info("# {0} is faulted", connection);
                return;
            }

            UpdateStatistics(connectionData);

            CheckPendingReceived(connection);
            CheckPendingSend(connection);
            CheckMissingSendCallback(connectionData, connection);
            CheckMissingReceiveCallback(connectionData, connection);
        }

        private void UpdateStatistics(ConnectionData connectionData)
        {
            var connection = connectionData.Connection;
            long totalBytesSent = connection.TotalBytesSent;
            long totalBytesReceived = connection.TotalBytesReceived;
            long pendingSend = connection.PendingSendBytes;
            long inSend = connection.InSendBytes;
            long pendingReceived = connection.PendingReceivedBytes;

            _sentSinceLastRun += totalBytesSent - connectionData.LastTotalBytesSent;
            _receivedSinceLastRun += totalBytesReceived - connectionData.LastTotalBytesReceived;
            
            _sentTotal += _sentSinceLastRun;
            _receivedTotal += _sentSinceLastRun;

            _pendingSendOnLastRun += pendingSend;
            _inSendOnLastRun += inSend;
            _pendingReceivedOnLastRun = pendingReceived;

            connectionData.LastTotalBytesSent = totalBytesSent;
            connectionData.LastTotalBytesReceived = totalBytesReceived;
        }

        private static void CheckMissingReceiveCallback(ConnectionData connectionData, IMonitoredTcpConnection connection)
        {
            bool inReceive = connection.InReceive;
            bool isReadyForReceive = connection.IsReadyForReceive;
            DateTime? lastReceiveStarted = connection.LastReceiveStarted;

            int sinceLastReceive = (int)(DateTime.UtcNow - lastReceiveStarted.GetValueOrDefault()).TotalMilliseconds;
            bool missingReceiveCallback = inReceive && isReadyForReceive && sinceLastReceive > 500;

            if (missingReceiveCallback && connectionData.LastMissingReceiveCallBack)
            {
                Log.Error("# {0} {1}ms since last Receive started. No completion callback received, but socket status is READY_FOR_RECEIVE",
                          connection, sinceLastReceive);
            }
            connectionData.LastMissingReceiveCallBack = missingReceiveCallback;
        }

        private void CheckMissingSendCallback(ConnectionData connectionData, IMonitoredTcpConnection connection)
        {
            // snapshot all data?
            bool inSend = connection.InSend;
            bool isReadyForSend = connection.IsReadyForSend;
            DateTime? lastSendStarted = connection.LastSendStarted;
            int inSendBytes = connection.InSendBytes;

            int sinceLastSend = (int)(DateTime.UtcNow - lastSendStarted.GetValueOrDefault()).TotalMilliseconds;
            bool missingSendCallback = inSend && isReadyForSend && sinceLastSend > 500;

            if (missingSendCallback && connectionData.LastMissingSendCallBack)
            {
                // _anySendBlockedOnLastRun = true;
                Log.Error(
                    "# {0} {1}ms since last send started. No completion callback received, but socket status is READY_FOR_SEND. In send: {2}",
                    connection, sinceLastSend, inSendBytes);
            }
            connectionData.LastMissingSendCallBack = missingSendCallback;
        }

        private static void CheckPendingSend(IMonitoredTcpConnection connection)
        {
            int pendingSendBytes = connection.PendingSendBytes;
            if (pendingSendBytes > 128 * 1024)
            {
                Log.Info("# {0} {1}kb pending send", connection, pendingSendBytes / 1024);
            }
        }

        private static void CheckPendingReceived(IMonitoredTcpConnection connection)
        {
            int pendingReceivedBytes = connection.PendingReceivedBytes;
            if (pendingReceivedBytes > 128 * 1024)
            {
                Log.Info("# {0} {1}kb are not dispatched", connection, pendingReceivedBytes / 1024);
            }
        }

        public bool IsSendBlocked()
        {
            return _anySendBlockedOnLastRun;
        }

        private class ConnectionData
        {
            public readonly IMonitoredTcpConnection Connection;
            public bool LastMissingSendCallBack;
            public bool LastMissingReceiveCallBack;
            public long LastTotalBytesSent;
            public long LastTotalBytesReceived;

            public ConnectionData(IMonitoredTcpConnection connection)
            {
                Connection = connection;
            }
        }
    }
}