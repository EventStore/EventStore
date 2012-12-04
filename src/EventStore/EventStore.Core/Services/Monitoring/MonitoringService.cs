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
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Monitoring.Utils;
using EventStore.Core.Services.Transport.Http.Codecs;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services.Monitoring
{
    [Flags]
    public enum StatsStorage
    {
        None = 0x0,       // only for tests
        Stream = 0x1,
        Csv = 0x2,
        StreamAndCsv = Stream | Csv
    }

    public class MonitoringService : IHandle<SystemMessage.SystemInit>,
                                     IHandle<SystemMessage.StateChangeMessage>,
                                     IHandle<SystemMessage.BecomeShuttingDown>,
                                     IHandle<MonitoringMessage.GetFreshStats>,
                                     IHandle<ClientMessage.CreateStreamCompleted>
    {
        private static readonly ILogger RegularLog = LogManager.GetLogger("REGULAR-STATS-LOGGER");
        private static readonly ILogger Log = LogManager.GetLoggerFor<MonitoringService>();

        private static readonly string StreamMetadata = string.Format("{{\"$maxAge\":{0}}}", (int)TimeSpan.FromDays(10).TotalMilliseconds);
        private static readonly TimeSpan MemoizePeriod = TimeSpan.FromSeconds(1);
        private static readonly IEnvelope NoopEnvelope = new NoopEnvelope();

        private readonly IPublisher _monitoringBus;
        private readonly IPublisher _statsCollectionBus;
        private readonly IPublisher _mainBus;
        private readonly StatsStorage _statsStorage;
        private readonly int _statsCollectionPeriodMs;
        private readonly SystemStatsHelper _systemStats;

        private string _lastWrittenCsvHeader;
        private DateTime _lastStatsRequestTime = DateTime.UtcNow;
        private StatsContainer _memoizedStats;
        private readonly Timer _timer;
        private readonly string _nodeStatsStream;
        private bool _statsStreamCreated;

        public MonitoringService(IPublisher monitoringBus,
                                 IPublisher statsCollectionBus,
                                 IPublisher mainBus,
                                 ICheckpoint writerCheckpoint,
                                 string dbPath,
                                 TimeSpan statsCollectionPeriod,
                                 IPEndPoint nodeEndpoint,
                                 StatsStorage statsStorage)
        {
            Ensure.NotNull(monitoringBus, "monitoringBus");
            Ensure.NotNull(statsCollectionBus, "statsCollectionBus");
            Ensure.NotNull(mainBus, "mainBus");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
            Ensure.NotNullOrEmpty(dbPath, "dbPath");
            Ensure.NotNull(nodeEndpoint, "nodeEndpoint");

            _monitoringBus = monitoringBus;
            _statsCollectionBus = statsCollectionBus;
            _mainBus = mainBus;
            _statsStorage = statsStorage;
            _statsCollectionPeriodMs = (int)statsCollectionPeriod.TotalMilliseconds;
            _systemStats = new SystemStatsHelper(Log, writerCheckpoint, dbPath);
            _nodeStatsStream = string.Format("{0}-{1}", SystemStreams.StatsStreamPrefix, nodeEndpoint);
            _timer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            _timer.Change(_statsCollectionPeriodMs, Timeout.Infinite);
        }

        public void OnTimerTick(object state)
        {
            CollectRegularStats();
            _timer.Change(_statsCollectionPeriodMs, Timeout.Infinite);
        }

        private void CollectRegularStats()
        {
            try
            {
                var stats = CollectStats();
                if (stats != null)
                {
                    var rawStats = stats.GetStats(useGrouping: false, useMetadata: false);

                    if ((_statsStorage & StatsStorage.Csv) != 0)
                        SaveStatsToCsvFile(rawStats);

                    if ((_statsStorage & StatsStorage.Stream) != 0)
                    {
                        if (_statsStreamCreated)
                            SaveStatsToStream(rawStats);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error on regular stats collection.");
            }
        }

        private StatsContainer CollectStats()
        {
            var statsContainer = new StatsContainer();
            try
            {
                statsContainer.Add(_systemStats.GetSystemStats());
                _statsCollectionBus.Publish(new MonitoringMessage.InternalStatsRequest(new StatsCollectorEnvelope(statsContainer)));
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error while collecting stats");
                statsContainer = null;
            }

            return statsContainer;
        }

        private void SaveStatsToCsvFile(Dictionary<string, object> rawStats)
        {
            var header = StatsCsvEncoder.GetHeader(rawStats);
            if (header != _lastWrittenCsvHeader)
            {
                _lastWrittenCsvHeader = header;
                RegularLog.Info(Environment.NewLine);
                RegularLog.Info(header);
            }

            var line = StatsCsvEncoder.GetLine(rawStats);
            RegularLog.Info(line);
        }

        private void SaveStatsToStream(Dictionary<string, object> rawStats)
        {
            var data = Codec.Json.To(rawStats);
            var @event = new Event(Guid.NewGuid(), SystemEventTypes.StatsCollection, true, Encoding.UTF8.GetBytes(data), null);
            var msg = new ClientMessage.WriteEvents(Guid.NewGuid(), NoopEnvelope,  true, _nodeStatsStream, ExpectedVersion.Any, @event);
            _mainBus.Publish(msg);
        }

        public void Handle(SystemMessage.StateChangeMessage message)
        {
            if ((_statsStorage & StatsStorage.Stream) == 0)
                return;

            if (_statsStreamCreated)
                return;

            switch (message.State)
            {
                case VNodeState.CatchingUp:
                case VNodeState.Clone:
                case VNodeState.Slave:
                case VNodeState.Master:
                {
                    CreateStatsStream();
                    break;
                }
                case VNodeState.Initializing:
                case VNodeState.Unknown:
                case VNodeState.Manager:
                case VNodeState.ShuttingDown:
                case VNodeState.Shutdown:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void CreateStatsStream()
        {
            var metadata = Encoding.UTF8.GetBytes(StreamMetadata);
            _mainBus.Publish(new ClientMessage.CreateStream(Guid.NewGuid(),
                                                            new PublishEnvelope(_monitoringBus),
                                                            true,
                                                            _nodeStatsStream,
                                                            Guid.NewGuid(),
                                                            true,
                                                            metadata));
        }

        public void Handle(ClientMessage.CreateStreamCompleted message)
        {
            switch (message.ErrorCode)
            {
                case OperationErrorCode.Success:
                case OperationErrorCode.WrongExpectedVersion: // already created
                {
                    Log.Trace("Created stats stream '{0}', code = {1}", _nodeStatsStream, message.ErrorCode);
                    _statsStreamCreated = true;
                    break;
                }
                case OperationErrorCode.PrepareTimeout:
                case OperationErrorCode.CommitTimeout:
                case OperationErrorCode.ForwardTimeout:
                {
                    Log.Debug("Failed to create stats stream '{0}'. Reason : {1}({2}). Retrying...", _nodeStatsStream, message.ErrorCode, message.Error);
                    CreateStatsStream();
                    break;
                }
                case OperationErrorCode.StreamDeleted:
                case OperationErrorCode.InvalidTransaction: // should not happen at all
                {
                    Log.Error("Monitoring service got unexpected response code when trying to create stats stream ({0}).", message.ErrorCode);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            try
            {
                _timer.Dispose();
                _systemStats.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // ok, no problem if already disposed
            }
        }

        public void Handle(MonitoringMessage.GetFreshStats message)
        {
            try
            {
                StatsContainer stats;
                if (!TryGetMemoizedStats(out stats))
                {
                    stats = CollectStats();
                    if (stats != null)
                    {
                        _memoizedStats = stats;
                        _lastStatsRequestTime = DateTime.UtcNow;
                    }
                }

                Dictionary<string, object> selectedStats = null;
                if (stats != null)
                {
                    selectedStats = stats.GetStats(message.UseGrouping, message.UseMetadata);
                    if (message.UseGrouping)
                        selectedStats = message.StatsSelector(selectedStats);
                }

                message.Envelope.ReplyWith(
                    new MonitoringMessage.GetFreshStatsCompleted(success: selectedStats != null, stats: selectedStats));
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error on getting fresh stats");
            }
        }

        private bool TryGetMemoizedStats(out StatsContainer stats)
        {
            if (_memoizedStats == null || DateTime.UtcNow - _lastStatsRequestTime > MemoizePeriod)
            {
                stats = null;
                return false;
            }
            else
            {
                stats = _memoizedStats;
                return true;
            }
        }
    }
}
