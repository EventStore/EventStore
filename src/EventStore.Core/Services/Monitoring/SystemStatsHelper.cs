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

namespace EventStore.Core.Services.Monitoring {
	public class SystemStatsHelper : IDisposable {
		internal static readonly Regex SpacesRegex = new Regex(@"[\s\t]+", RegexOptions.Compiled);

		private readonly ILogger _log;
		private readonly ICheckpoint _writerCheckpoint;
		private readonly string _dbPath;
		private PerfCounterHelper _perfCounter;
		private bool _giveup;

		public SystemStatsHelper(ILogger log, ICheckpoint writerCheckpoint, string dbPath) {
			Ensure.NotNull(log, "log");
			Ensure.NotNull(writerCheckpoint, "writerCheckpoint");

			_log = log;
			_writerCheckpoint = writerCheckpoint;
			_perfCounter = new PerfCounterHelper(_log);
			_dbPath = dbPath;
		}

		public IDictionary<string, object> GetSystemStats() {
			var stats = new Dictionary<string, object>();
			GetPerfCounterInformation(stats, 0);
			var process = Process.GetCurrentProcess();

			var diskIo = DiskIo.GetDiskIo(process.Id, _log);
			if (diskIo != null) {
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

			stats["es-checksum"] = _writerCheckpoint.Read();
			stats["es-checksumNonFlushed"] = _writerCheckpoint.ReadNonFlushed();

			var drive = EsDriveInfo.FromDirectory(_dbPath, _log);
			if (drive != null) {
				Func<string, string, string> driveStat = (diskName, stat) =>
					string.Format("sys-drive-{0}-{1}", diskName.Replace("\\", "").Replace(":", ""), stat);
				stats[driveStat(drive.DiskName, "availableBytes")] = drive.AvailableBytes;
				stats[driveStat(drive.DiskName, "totalBytes")] = drive.TotalBytes;
				stats[driveStat(drive.DiskName, "usage")] = drive.Usage;
				stats[driveStat(drive.DiskName, "usedBytes")] = drive.UsedBytes;
			}

			Func<string, string, string> queueStat = (queueName, stat) =>
				string.Format("es-queue-{0}-{1}", queueName, stat);
			var queues = QueueMonitor.Default.GetStats();
			foreach (var queue in queues) {
				stats[queueStat(queue.Name, "queueName")] = queue.Name;
				stats[queueStat(queue.Name, "groupName")] = queue.GroupName ?? string.Empty;
				stats[queueStat(queue.Name, "avgItemsPerSecond")] = queue.AvgItemsPerSecond;
				stats[queueStat(queue.Name, "avgProcessingTime")] = queue.AvgProcessingTime;
				stats[queueStat(queue.Name, "currentIdleTime")] = queue.CurrentIdleTime.HasValue
					? queue.CurrentIdleTime.Value.ToString("G", CultureInfo.InvariantCulture)
					: null;
				stats[queueStat(queue.Name, "currentItemProcessingTime")] = queue.CurrentItemProcessingTime.HasValue
					? queue.CurrentItemProcessingTime.Value.ToString("G", CultureInfo.InvariantCulture)
					: null;
				stats[queueStat(queue.Name, "idleTimePercent")] = queue.IdleTimePercent;
				stats[queueStat(queue.Name, "length")] = queue.Length;
				stats[queueStat(queue.Name, "lengthCurrentTryPeak")] = queue.LengthCurrentTryPeak;
				stats[queueStat(queue.Name, "lengthLifetimePeak")] = queue.LengthLifetimePeak;
				stats[queueStat(queue.Name, "totalItemsProcessed")] = queue.TotalItemsProcessed;
				stats[queueStat(queue.Name, "inProgressMessage")] = queue.InProgressMessageType != null
					? queue.InProgressMessageType.Name
					: "<none>";
				stats[queueStat(queue.Name, "lastProcessedMessage")] = queue.LastProcessedMessageType != null
					? queue.LastProcessedMessageType.Name
					: "<none>";
			}

			return stats;
		}

		private void GetPerfCounterInformation(Dictionary<string, object> stats, int count) {
			if (_giveup)
				return;
			var process = Process.GetCurrentProcess();
			try {
				_perfCounter.RefreshInstanceName();

				var procCpuUsage = _perfCounter.GetProcCpuUsage();

				stats["proc-startTime"] = process.StartTime.ToUniversalTime().ToString("O");
				stats["proc-id"] = process.Id;
				stats["proc-mem"] = new StatMetadata(process.WorkingSet64, "Process", "Process Virtual Memory");
				stats["proc-cpu"] = new StatMetadata(procCpuUsage, "Process", "Process Cpu Usage");
				stats["proc-cpuScaled"] = new StatMetadata(procCpuUsage / Environment.ProcessorCount, "Process",
					"Process Cpu Usage Scaled by Logical Processor Count");
				stats["proc-threadsCount"] = _perfCounter.GetProcThreadsCount();
				stats["proc-contentionsRate"] = _perfCounter.GetContentionsRateCount();
				stats["proc-thrownExceptionsRate"] = _perfCounter.GetThrownExceptionsRate();

				stats["sys-cpu"] = _perfCounter.GetTotalCpuUsage();
				stats["sys-freeMem"] = GetFreeMem();

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
			} catch (InvalidOperationException) {
				_log.Info("Received error reading counters. Attempting to rebuild.");
				_perfCounter = new PerfCounterHelper(_log);
				_giveup = count > 10;
				if (_giveup)
					_log.Error("Maximum rebuild attempts reached. Giving up on rebuilds.");
				else
					GetPerfCounterInformation(stats, count + 1);
			}
		}

		///<summary>
		///Free system memory in bytes
		///</summary>
		private long GetFreeMem() {
			switch (OS.OsFlavor) {
				case OsFlavor.Windows:
					return _perfCounter.GetFreeMemory();
				case OsFlavor.Linux:
					return GetFreeMemOnLinux();
				case OsFlavor.MacOS:
					return GetFreeMemOnOSX();
				case OsFlavor.BSD:
					return GetFreeMemOnBSD();
				default:
					return -1;
			}
		}

		private long GetFreeMemOnLinux() {
			string meminfo = null;
			try {
				meminfo = ShellExecutor.GetOutput("free", "-b");
				var meminfolines = meminfo.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
				var ourline = meminfolines[1];
				var trimmedLine = SpacesRegex.Replace(ourline, " ");
				var freeRamStr = trimmedLine.Split(' ')[3];
				return long.Parse(freeRamStr);
			} catch (Exception ex) {
				_log.DebugException(ex, "Could not get free mem on linux, received memory info raw string: [{meminfo}]",
					meminfo);
				return -1;
			}
		}

		// http://www.cyberciti.biz/files/scripts/freebsd-memory.pl.txt
		private long GetFreeMemOnBSD() {
			try {
				var sysctl = ShellExecutor.GetOutput("sysctl",
					"-n hw.physmem hw.pagesize vm.stats.vm.v_free_count vm.stats.vm.v_cache_count vm.stats.vm.v_inactive_count");
				var sysctlStats = sysctl.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
				long pageSize = long.Parse(sysctlStats[1]);
				long freePages = long.Parse(sysctlStats[2]);
				long cachePages = long.Parse(sysctlStats[3]);
				long inactivePages = long.Parse(sysctlStats[4]);
				return pageSize * (freePages + cachePages + inactivePages);
			} catch (Exception ex) {
				_log.DebugException(ex, "Could not get free memory on BSD.");
				return -1;
			}
		}


		private long GetFreeMemOnOSX() {
			int freePages = 0;
			try {
				var vmstat = ShellExecutor.GetOutput("vm_stat");
				var sysctlStats = vmstat.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
				foreach (var line in sysctlStats) {
					var l = line.Substring(0, line.Length - 1);
					var pieces = l.Split(':');
					if (pieces.Length == 2) {
						if (pieces[0].Trim().ToLower() == "pages free") {
							freePages = int.Parse(pieces[1]);
							break;
						}
					}
				}

				return 4096 * freePages;
			} catch (Exception ex) {
				_log.DebugException(ex, "Could not get free memory on OSX.");
				return -1;
			}
		}

		public void Dispose() {
			_perfCounter.Dispose();
		}
	}
}
