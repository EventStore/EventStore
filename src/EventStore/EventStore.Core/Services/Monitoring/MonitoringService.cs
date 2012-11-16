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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Monitoring.Utils;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Codecs;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services.Monitoring
{
    public class MonitoringService : IHandle<SystemMessage.SystemInit>,
                                     IHandle<SystemMessage.SystemStart>,
                                     IHandle<SystemMessage.BecomeShuttingDown>,
                                     IHandle<MonitoringMessage.GetFreshStats>,
                                     IHandle<ClientMessage.CreateStreamCompleted>,
                                     IHandle<ClientMessage.WriteEventsCompleted>
    {
        private static readonly ILogger RegularLog = LogManager.GetLogger("REGULAR-STATS-LOGGER");
        private static readonly ILogger Log = LogManager.GetLoggerFor<MonitoringService>();

        private readonly IPublisher _inputBus;
        private readonly IPublisher _internalStatsCollectionBus;
        private readonly IPublisher _outputBus;
        private readonly int _statsCollectionPeriodMs;
        private readonly Timer _timer;
        private readonly SystemStatsHelper _systemStats;
        
        private string _lastWrittenCsvHeader;

        private DateTime _lastStatsRequestTime = DateTime.UtcNow;
        private StatsContainer _memoizedStats;
        private const int _memoizedSeconds = 1;

        private readonly string _nodeStatsStream;
        private readonly JsonCodec _jsonCodec;
        private const string StreamMetadata = @"{""maxAge"": 10 }";  // 10 days           864000
        private bool _statsStreamCreated;

        public MonitoringService(IPublisher inputBus, IPublisher internalStatsCollectionBus, IPublisher outputBus, ICheckpoint writerCheckpoint, string dbPath, TimeSpan statsCollectionPeriod, IPEndPoint nodeEndpoint)
        {
            Ensure.NotNull(inputBus, "inputBus");
            Ensure.NotNull(internalStatsCollectionBus, "internalStatsCollectionBus");
            Ensure.NotNull(outputBus, "outputBus");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
            Ensure.NotNullOrEmpty(dbPath, "dbPath");
            Ensure.NotNull(nodeEndpoint, "nodeEndpoint");

            _inputBus = inputBus;
            _internalStatsCollectionBus = internalStatsCollectionBus;
            _outputBus = outputBus;
            _statsCollectionPeriodMs = (int)statsCollectionPeriod.TotalMilliseconds;
            _timer = new Timer(OnTimerTicked, null, Timeout.Infinite, Timeout.Infinite);
            _systemStats = new SystemStatsHelper(Log, writerCheckpoint, dbPath);
            _nodeStatsStream = SystemStreams.StatsStreamPrefix + nodeEndpoint.ToString();
            _jsonCodec = new JsonCodec();
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            _timer.Change(_statsCollectionPeriodMs, Timeout.Infinite);
        }

        private void OnTimerTicked(object state)
        {
            CollectRegularStats();
            _timer.Change(_statsCollectionPeriodMs, Timeout.Infinite);
        }

        private void CollectRegularStats()
        {
            try
            {
                var stats = GetStats();
                if (stats != null)
                {
                    var rawStats = stats.GetStats(useGrouping: false, useMetadata: false);
                    if (_statsStreamCreated)
                    {
                        
                        SaveStatsToStream(rawStats);
                    }
                    else
                    {
                        //todo MM: dont skip first stats
                        CreateStatsStream();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "error on regular stats collection");
            }
        }

        private StatsContainer GetStats()
        {
            var statsContainer = new StatsContainer();
            try
            {
                statsContainer.Add(_systemStats.GetSystemStats());
                _internalStatsCollectionBus.Publish(new MonitoringMessage.InternalStatsRequest(new StatsCollectorEnvelope(statsContainer)));
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error while collecting stats");
                statsContainer = null;
            }

            return statsContainer;
        }

        private void SaveStatsToStream(Dictionary<string, object> rawStats)
        {
            var data = _jsonCodec.To(rawStats);
            var @event = new Event(Guid.NewGuid(), SystemEventTypes.StatsCollection, true, Encoding.UTF8.GetBytes(data), null);
            var msg = new ClientMessage.WriteEvents(Guid.NewGuid(),
                                                    new PublishEnvelope(_inputBus, crossThread: true),
                                                    false,
                                                    _nodeStatsStream,
                                                    ExpectedVersion.Any,
                                                    @event);

            _outputBus.Publish(msg);
        }

        public void Handle(SystemMessage.SystemStart message)
        {

        }

        private void CreateStatsStream()
        {
            var envelope = new PublishEnvelope(_inputBus, crossThread: true);
            var metadata = Encoding.UTF8.GetBytes(StreamMetadata);
            var createStream = new ClientMessage.CreateStream(Guid.NewGuid(), envelope, false, _nodeStatsStream, true, metadata);
            _outputBus.Publish(createStream);
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _timer.Dispose();
            _systemStats.Dispose();
        }

        public void Handle(MonitoringMessage.GetFreshStats message)
        {
            try
            {
                StatsContainer stats;
                if (!TryGetMemoizedStats(out stats))
                {
                    stats = GetStats();
                    if (stats != null)
                        MemoizeStats(stats);
                }
            
                Dictionary<string, object> selectedStats = null;
                if (stats != null)
                {
                    selectedStats = stats.GetStats(message.UseGrouping, message.UseMetadata);
                    if (message.UseGrouping)
                        selectedStats = message.StatsSelector(selectedStats) ;
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
            if (_memoizedStats == null || (DateTime.UtcNow - _lastStatsRequestTime).TotalSeconds > _memoizedSeconds)
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

        private void MemoizeStats(StatsContainer stats)
        {
            Ensure.NotNull(stats, "stats");

            _memoizedStats = stats;
            _lastStatsRequestTime = DateTime.UtcNow;
        }

        public void Handle(ClientMessage.CreateStreamCompleted message)
        {
            switch (message.ErrorCode)
            {
                case OperationErrorCode.Success:
                    _statsStreamCreated = true;
                    break;
                case OperationErrorCode.PrepareTimeout:
                case OperationErrorCode.CommitTimeout:
                case OperationErrorCode.ForwardTimeout:
                case OperationErrorCode.InvalidTransaction: // should not happen at all
                    CreateStatsStream();
                    break;
                case OperationErrorCode.WrongExpectedVersion:
                case OperationErrorCode.StreamDeleted:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Handle(ClientMessage.WriteEventsCompleted message)
        {
            if (message.ErrorCode == OperationErrorCode.Success)
                return;

            //todo MM : save stats to file
            //SaveStatsToCsvFile(???);
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
    }
}
