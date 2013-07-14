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
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Monitoring.Utils;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Transport.Tcp;

namespace EventStore.Core.Services.Monitoring
{
    public class SystemStatsHelper : IDisposable
    {
        internal static readonly Regex SpacesRegex = new Regex(@"[\s\t]+", RegexOptions.Compiled);

        private readonly ILogger _log;
        private readonly ICheckpoint _writerCheckpoint;
        private readonly string _dbPath;
        private readonly PerfCounterHelper _perfCounter;

        public SystemStatsHelper(ILogger log, ICheckpoint writerCheckpoint, string dbPath)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");

            _log = log;
            _writerCheckpoint = writerCheckpoint;
            _perfCounter = new PerfCounterHelper(_log);
            _dbPath = dbPath;
        }

        public IDictionary<string, object> GetSystemStats()
        {
            var stats = new Dictionary<string, object>();
            var process = Process.GetCurrentProcess();

            stats["proc-startTime"] = process.StartTime.ToUniversalTime().ToString("O");
            stats["proc-id"] = process.Id;
            stats["proc-mem"] = new StatMetadata(process.WorkingSet64, "Process", "Process Virtual Memory");
            stats["proc-cpu"] = new StatMetadata(_perfCounter.GetProcCpuUsage(), "Process", "Process Cpu Usage");
            stats["proc-threadsCount"] = _perfCounter.GetProcThreadsCount();
            stats["proc-contentionsRate"] = _perfCounter.GetContentionsRateCount();
            stats["proc-thrownExceptionsRate"] = _perfCounter.GetThrownExceptionsRate();

            stats["sys-cpu"] = _perfCounter.GetTotalCpuUsage();
            stats["sys-freeMem"] = GetFreeMem();

            var diskIo = DiskIo.GetDiskIo(process.Id, _log);
            if (diskIo != null)
            {
                stats["proc-diskIo-readBytes"] = diskIo.ReadBytes;
                stats["proc-diskIo-writtenBytes"] = diskIo.WrittenBytes;
                stats["proc-diskIo-readOps"] = diskIo.ReadOps;
                stats["proc-diskIo-writeOps"] = diskIo.WriteOps;
            }

            var tcp = TcpConnectionMonitor.Default.GetTcpStats();
            stats["proc-tcp-connections"] = tcp.Connections;
            stats["proc-tcp-receivingSpeed"] = tcp.ReceivingSpeed;
            stats["proc-tcp-sendingSpeed"] = tcp.SendingSpeed;
            stats["proc-tcp-inSend"] = tcp.InSend;
            stats["proc-tcp-measureTime"] = tcp.MeasureTime;
            stats["proc-tcp-pendingReceived"] = tcp.PendingReceived;
            stats["proc-tcp-pendingSend"] = tcp.PendingSend;
            stats["proc-tcp-receivedBytesSinceLastRun"] = tcp.ReceivedBytesSinceLastRun;
            stats["proc-tcp-receivedBytesTotal"] = tcp.ReceivedBytesTotal;
            stats["proc-tcp-sentBytesSinceLastRun"] = tcp.SentBytesSinceLastRun;
            stats["proc-tcp-sentBytesTotal"] = tcp.SentBytesTotal;

            var gcStats = _perfCounter.GetGcStats();
            stats["proc-gc-allocationSpeed"] = gcStats.AllocationSpeed;
            stats["proc-gc-gen0ItemsCount"] = gcStats.Gen0ItemsCount;
            stats["proc-gc-gen0Size"] = gcStats.Gen0Size;
            stats["proc-gc-gen1ItemsCount"] = gcStats.Gen1ItemsCount;
            stats["proc-gc-gen1Size"] = gcStats.Gen1Size;
            stats["proc-gc-gen2ItemsCount"] = gcStats.Gen2ItemsCount;
            stats["proc-gc-gen2Size"] = gcStats.Gen2Size;
            stats["proc-gc-largeHeapSize"] = gcStats.LargeHeapSize;
            stats["proc-gc-timeInGc"] = gcStats.TimeInGc;
            stats["proc-gc-totalBytesInHeaps"] = gcStats.TotalBytesInHeaps;

            stats["es-checksum"] = _writerCheckpoint.Read();
            stats["es-checksumNonFlushed"] = _writerCheckpoint.ReadNonFlushed();

            var drive = EsDriveInfo.FromDirectory(_dbPath, _log);
            if (drive != null)
            {
                Func<string, string, string> driveStat = (diskName, stat) => string.Format("sys-drive-{0}-{1}", diskName.Replace("\\","").Replace(":",""), stat);
                stats[driveStat(drive.DiskName, "availableBytes")] = drive.AvailableBytes;
                stats[driveStat(drive.DiskName, "totalBytes")] = drive.TotalBytes;
                stats[driveStat(drive.DiskName, "usage")] = drive.Usage;
                stats[driveStat(drive.DiskName, "usedBytes")] = drive.UsedBytes;
            }

            Func<string, string, string> queueStat = (queueName, stat) => string.Format("es-queue-{0}-{1}", queueName, stat);
            var queues = QueueMonitor.Default.GetStats();
            foreach (var queue in queues)
            {
                stats[queueStat(queue.Name, "queueName")] = queue.Name;
                stats[queueStat(queue.Name, "groupName")] = queue.GroupName ?? string.Empty;
                stats[queueStat(queue.Name, "avgItemsPerSecond")] = queue.AvgItemsPerSecond;
                stats[queueStat(queue.Name, "avgProcessingTime")] = queue.AvgProcessingTime;
                stats[queueStat(queue.Name, "currentIdleTime")] = queue.CurrentIdleTime.HasValue ? queue.CurrentIdleTime.Value.ToString("G", CultureInfo.InvariantCulture) : null;
                stats[queueStat(queue.Name, "currentItemProcessingTime")] = queue.CurrentItemProcessingTime.HasValue ? queue.CurrentItemProcessingTime.Value.ToString("G", CultureInfo.InvariantCulture) : null;
                stats[queueStat(queue.Name, "idleTimePercent")] = queue.IdleTimePercent;
                stats[queueStat(queue.Name, "length")] = queue.Length;
                stats[queueStat(queue.Name, "lengthCurrentTryPeak")] = queue.LengthCurrentTryPeak;
                stats[queueStat(queue.Name, "lengthLifetimePeak")] = queue.LengthLifetimePeak;
                stats[queueStat(queue.Name, "totalItemsProcessed")] = queue.TotalItemsProcessed;
                stats[queueStat(queue.Name, "inProgressMessage")] = queue.InProgressMessageType != null ? queue.InProgressMessageType.Name : "<none>";
                stats[queueStat(queue.Name, "lastProcessedMessage")] = queue.LastProcessedMessageType != null ? queue.LastProcessedMessageType.Name : "<none>";
            }

            return stats;
        }

        private long GetFreeMem()
        {
            switch (OS.OsFlavor)
            {
                case OsFlavor.Windows:
                    return _perfCounter.GetFreeMemory();
                case OsFlavor.Linux:
                case OsFlavor.MacOS:
                    return GetFreeMemOnLinux();
                case OsFlavor.BSD:
                    return GetFreeMemOnBSD();
                default:
                    return -1;
            }
        }

        private long GetFreeMemOnLinux()
        {
            string meminfo = null;
            try
            {
                meminfo = ShellExecutor.GetOutput("free", "-b");
                var meminfolines = meminfo.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                var ourline = meminfolines[1];
                var trimmedLine = SpacesRegex.Replace(ourline, " ");
                var freeRamStr = trimmedLine.Split(' ')[3];
                return long.Parse(freeRamStr);
            }
            catch (Exception ex)
            {
                _log.DebugException(ex, "Couldn't get free mem on linux, received memory info raw string: [{0}]", meminfo);
                return -1;
            }
        }

        private long GetFreeMemOnBSD()
        {
            try
            {
                var sysctl = ShellExecutor.GetOutput("sysctl", "-n hw.physmem hw.pagesize vm.stats.vm.v_free_count vm.stats.vm.v_cache_count vm.stats.vm.v_inactive_count");
                var sysctlStats = sysctl.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                long pageSize = long.Parse(sysctlStats[1]);
                long freePages = long.Parse(sysctlStats[2]);
                long cachePages = long.Parse(sysctlStats[3]);
                long inactivePages = long.Parse(sysctlStats[4]);
                return pageSize * (freePages + cachePages + inactivePages);
            }
            catch (Exception ex)
            {
                _log.DebugException(ex, "Couldn't get free memory on BSD.");
                return -1;
            }
        }

        public void Dispose()
        {
            _perfCounter.Dispose();
        }
    }
}