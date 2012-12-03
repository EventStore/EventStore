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
            var drive = EsDriveInfo.FromDirectory(_dbPath, _log);
            var diskIo = DiskIo.GetDiskIo(process.Id, _log);
            var tcp = TcpConnectionMonitor.Default.GetTcpStats();
            var queues = QueueMonitor.Default.GetStats();

            var checksum = _writerCheckpoint.Read();
            var checksumNonFlushed = _writerCheckpoint.ReadNonFlushed();
            var workingSetMemory = process.WorkingSet64;
            var startTime = process.StartTime.ToUniversalTime().ToString("O");
            var procId = process.Id;

            var totalCpu = _perfCounter.GetTotalCpuUsage();
            var procCpu = _perfCounter.GetProcCpuUsage();
            var threadsCount = _perfCounter.GetProcThreadsCount();
            var freeMem = OS.IsLinux ? GetFreeMemOnLinux() : _perfCounter.GetFreeMemory();
            var gcStats = _perfCounter.GetGcStats();
            var thrownExceptionsRate = _perfCounter.GetThrownExceptionsRate();
            var contentionsRate = _perfCounter.GetContentionsRateCount();

            stats["proc-startTime"] = startTime;
            stats["proc-id"] = procId;
            stats["proc-mem"] = new StatMetadata(workingSetMemory, "Process", "Process Virtual Memory");
            stats["proc-cpu"] = new StatMetadata(procCpu, "Process", "Process Cpu Usage");
            stats["proc-threadsCount"] = threadsCount;
            stats["proc-contentionsRate"] = new StatMetadata(contentionsRate, "Process", "Contentions/s");
            stats["proc-thrownExceptionsRate"] = new StatMetadata(thrownExceptionsRate, "Process", "Thrown Exceptions/s");

            stats["sys-cpu"] = new StatMetadata(totalCpu, "Machine", "Machine CPU Usage");
            stats["sys-freeMem"] = freeMem;

            stats["proc-diskIo-readBytes"] = new StatMetadata(diskIo.ReadBytes, "Disk IO", "Disk Read Bytes");
            stats["proc-diskIo-writtenBytes"] = new StatMetadata(diskIo.WrittenBytes, "Disk IO", "Disk Written Bytes");
            stats["proc-diskIo-readOps"] = diskIo.ReadOps;
            stats["proc-diskIo-writeOps"] = diskIo.WriteOps;
            stats["proc-diskIo-readBytesFriendly"] = diskIo.ReadBytesFriendly;
            stats["proc-diskIo-readOpsFriendly"] = diskIo.ReadOpsFriendly;
            stats["proc-diskIo-writeOpsFriendly"] = diskIo.WriteOpsFriendly;
            stats["proc-diskIo-writtenBytesFriendly"] = diskIo.WrittenBytesFriendly;

            stats["proc-tcp-connections"] = new StatMetadata(tcp.Connections, "Tcp", "Tcp Connections");
            stats["proc-tcp-receivingSpeed"] = new StatMetadata(tcp.ReceivingSpeed, "Tcp", "Tcp Receiving Speed");
            stats["proc-tcp-sendingSpeed"] = new StatMetadata(tcp.SendingSpeed, "Tcp", "Tcp Sending Speed");
            stats["proc-tcp-inSend"] = new StatMetadata(tcp.InSend, "Tcp", "Tcp In Send");
            stats["proc-tcp-measureTime"] = tcp.MeasureTime;
            stats["proc-tcp-pendingReceived"] = new StatMetadata(tcp.PendingReceived, "Tcp", "Tcp Pending Received");
            stats["proc-tcp-pendingSend"] = new StatMetadata(tcp.PendingSend, "Tcp", "Tcp Pending Send");
            stats["proc-tcp-receivedBytesSinceLastRun"] = tcp.ReceivedBytesSinceLastRun;
            stats["proc-tcp-receivedBytesTotal"] = tcp.ReceivedBytesTotal;
            stats["proc-tcp-sentBytesSinceLastRun"] = tcp.SentBytesSinceLastRun;
            stats["proc-tcp-sentBytesTotal"] = tcp.SentBytesTotal;
            stats["proc-tcp-measureTimeFriendly"] = tcp.MeasureTimeFriendly;
            stats["proc-tcp-receivedBytesTotalFriendly"] = tcp.ReceivedBytesTotalFriendly;
            stats["proc-tcp-receivingSpeedFriendly"] = tcp.ReceivingSpeedFriendly;
            stats["proc-tcp-sendingSpeedFriendly"] = tcp.SendingSpeedFriendly;
            stats["proc-tcp-sentBytesTotalFriendly"] = tcp.SentBytesTotalFriendly;

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

            stats["es-checksum"] = checksum;
            stats["es-checksumNonFlushed"] = checksumNonFlushed;

            if (drive != null)
            {
                Func<string, string, string> driveStat = (diskName, stat) => string.Format("sys-drive-{0}-{1}", diskName, stat);
                stats[driveStat(drive.DiskName, "availableBytes")] = drive.AvailableBytes;
                stats[driveStat(drive.DiskName, "totalBytes")] = drive.TotalBytes;
                stats[driveStat(drive.DiskName, "usage")] = drive.Usage;
                stats[driveStat(drive.DiskName, "usedBytes")] = drive.UsedBytes;
                stats[driveStat(drive.DiskName, "availableBytesFriendly")] = drive.AvailableBytesFriendly;
                stats[driveStat(drive.DiskName, "totalBytesFriendly")] = drive.TotalBytesFriendly;
                stats[driveStat(drive.DiskName, "usedBytesFriendly")] = drive.UsedBytesFriendly;
            }

            Func<string, string, string> queueStat = (queueName, stat) => string.Format("es-queue-{0}-{1}", queueName, stat);
            foreach (var queue in queues)
            {
                stats[queueStat(queue.Name, "queueName")] = queue.Name;
                stats[queueStat(queue.Name, "avgItemsPerSecond")] = queue.AvgItemsPerSecond;
                stats[queueStat(queue.Name, "avgProcessingTime")] = new StatMetadata(queue.AvgProcessingTime, "Queue Stats", queue.Name +  " Avg Proc Time");
                stats[queueStat(queue.Name, "currentIdleTime")] = queue.CurrentIdleTime.HasValue ? queue.CurrentIdleTime.Value.ToString("G", CultureInfo.InvariantCulture) : null;
                stats[queueStat(queue.Name, "currentItemProcessingTime")] = queue.CurrentItemProcessingTime.HasValue ? queue.CurrentItemProcessingTime.Value.ToString("G", CultureInfo.InvariantCulture) : null;
                stats[queueStat(queue.Name, "idleTimePercent")] = queue.IdleTimePercent;
                stats[queueStat(queue.Name, "length")] = queue.Length;
                stats[queueStat(queue.Name, "lengthCurrentTryPeak")] = queue.LengthCurrentTryPeak;
                stats[queueStat(queue.Name, "lengthLifetimePeak")] = queue.LengthLifetimePeak;
                stats[queueStat(queue.Name, "totalItemsProcessed")] = queue.TotalItemsProcessed;
                stats[queueStat(queue.Name, "lengthLifetimePeakFriendly")] = queue.LengthLifetimePeakFriendly;
            }

            return stats;
        }

        private long GetFreeMemOnLinux()
        {
            try
            {
                var meminfo = ShellExecutor.GetOutput("free", "-b");
                var meminfolines = meminfo.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                var ourline = meminfolines[1];
                var spaces = new Regex(@"[\s\t]+", RegexOptions.Compiled);
                var trimmedLine = spaces.Replace(ourline, " ");
                var freeRamStr = trimmedLine.Split(' ')[3];
                return long.Parse(freeRamStr);
            }
            catch (Exception ex)
            {
                _log.DebugException(ex, "Couldn't get free mem on linux.");
                return -1;
            }
        }

        public void Dispose()
        {
            _perfCounter.Dispose();
        }
    }
}