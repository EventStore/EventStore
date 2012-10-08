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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Monitoring.Utils;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Services.Monitoring
{
    public class MonitoringService : IHandle<SystemMessage.SystemInit>,
                                     IHandle<SystemMessage.BecomeShuttingDown>,
                                     IHandle<MonitoringMessage.GetFreshStats>
    {
        private static readonly ILogger RegularLog = LogManager.GetLogger("REGULAR-STATS-LOGGER");
        private static readonly ILogger Log = LogManager.GetLoggerFor<MonitoringService>();

        private readonly IPublisher _servicesBus;
        private readonly int _statsCollectionPeriodMs;
        private readonly Timer _timer;
        private readonly SystemStatsHelper _systemStats;
        private string _lastWrittenCsvHeader;

        private DateTime _lastStatsRequestTime = DateTime.UtcNow;
        private StatsContainer _memoizedStats;
        private const int _memoizedSeconds = 1;

        public MonitoringService(IPublisher inputBus,
                                 IPublisher servicesBus,
                                 ICheckpoint writerCheckpoint,
                                 TimeSpan statsCollectionPeriod)
        {
            Ensure.NotNull(inputBus, "inputBus");
            Ensure.NotNull(servicesBus, "servicesBus");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");

            _servicesBus = servicesBus;
            _statsCollectionPeriodMs = (int)statsCollectionPeriod.TotalMilliseconds;
            _timer = new Timer(OnTimerTicked, null, Timeout.Infinite, Timeout.Infinite);
            _systemStats = new SystemStatsHelper(Log, writerCheckpoint);
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
            catch (Exception ex)
            {
                Log.ErrorException(ex, "error on regular stats collection");
            }
        }


        public void Handle(SystemMessage.SystemInit message)
        {
            _timer.Change(_statsCollectionPeriodMs, Timeout.Infinite);
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

        private StatsContainer GetStats()
        {
            var statsContainer = new StatsContainer();
            try
            {
                statsContainer.Add(_systemStats.GetSystemStats());
                _servicesBus.Publish(new MonitoringMessage.InternalStatsRequest(new StatsCollectorEnvelope(statsContainer)));
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error while collecting stats");
                statsContainer = null;
            }

            return statsContainer;
        }
    }
}
