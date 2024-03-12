// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace System.Runtime;

public enum RuntimeOSPlatform {
	Windows,
	OSX,
	Linux,
	FreeBSD
}

record LoadAverages(double OneMinute, double FiveMinutes, double FifteenMinutes);

record CpuStats(float Usage, float IdleTime, float TotalTime, LoadAverages LoadAverages);

public static class SystemRuntimeStats {
	public static readonly RuntimeOSPlatform OsPlatform;
	
	static SystemRuntimeStats() {
		OsPlatform = GetPlatform();
		
		return;

		static RuntimeOSPlatform GetPlatform() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				return RuntimeOSPlatform.OSX;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				return RuntimeOSPlatform.Linux;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return RuntimeOSPlatform.Windows;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
				return RuntimeOSPlatform.FreeBSD;
		
			throw new NotSupportedException("Operating system not supported");
		}
	}

	public static ulong                                                         TotalPhysicalMemory  => GetTotalPhysicalMemory();
	public static ulong                                                         TotalAvailableMemory => GetTotalAvailableMemory();
	public static ulong                                                         TotalFreeMemory      => GetTotalFreeMemory();
	public static (double OneMinute, double FiveMinutes, double FifteenMinutes) LoadAverages         => GetLoadAverages();
	public static (float Usage, float IdleTime, float TotalTime)                CpuStats             => GetCpuStats();
	
	public static ulong GetTotalPhysicalMemory() {
		return OsPlatform switch {
			RuntimeOSPlatform.Windows => GetTotalPhysicalMemoryWindows(),
			RuntimeOSPlatform.OSX     => GetTotalPhysicalMemoryMac(),
			RuntimeOSPlatform.Linux   => GetTotalPhysicalMemoryLinux(),
			_                         => throw new NotSupportedException("Operating system not supported")
		};

		[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
		static ulong GetTotalPhysicalMemoryWindows() {
			using var counter = new PerformanceCounter("Memory", "Total Physical Memory");
			return Convert.ToUInt64(counter.NextValue());
		}
		
		static ulong GetTotalPhysicalMemoryMac() {
			var output = ExecuteBashCommand("sysctl -n hw.memsize");
			return Convert.ToUInt64(output);
		}
		
		static ulong GetTotalPhysicalMemoryLinux() {
			var output = ExecuteBashCommand("cat /proc/meminfo | grep MemTotal");
			var parts  = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			return Convert.ToUInt64(parts[1]) * 1024; // Convert from KB to bytes
		}
	}
	
	public static ulong GetTotalAvailableMemory() {
		return OsPlatform switch {
			RuntimeOSPlatform.Windows => GetAvailableMemoryWindows(),
			RuntimeOSPlatform.OSX     => GetAvailableMemoryMac(),
			RuntimeOSPlatform.Linux   => GetAvailableMemoryLinux(),
			_                       => throw new NotSupportedException("Operating system not supported")
		};
		
		static ulong GetAvailableMemoryWindows() {
			#pragma warning disable CA1416
			using var counter = new PerformanceCounter("Memory", "Total Visible Memory Size");
			return Convert.ToUInt64(counter.NextValue());
			#pragma warning restore CA1416
		}

		static ulong GetAvailableMemoryMac() {
			var output = ExecuteBashCommand("vm_stat | grep 'Pages inactive:' | awk '{print $3}'");
			return Convert.ToUInt64(output.TrimEnd('.')) * 4096; // Pages are 4KB in size
		}

		static ulong GetAvailableMemoryLinux() {
			var output = ExecuteBashCommand("free -b | grep Mem | awk '{print $4}'");
			return Convert.ToUInt64(output);
		}
	}

	public static ulong GetTotalFreeMemory() {
		return OsPlatform switch {
			RuntimeOSPlatform.Windows => GetFreeMemoryWindows(),
			RuntimeOSPlatform.OSX     => GetFreeMemoryMac(),
			RuntimeOSPlatform.Linux   => GetFreeMemoryLinux(),
			_                         => throw new NotSupportedException("Operating system not supported")
		};
		
		// // could this work on all platforms? maybe 
		// static ulong GetFreeMemory() {
		// 	using var process = Process.GetCurrentProcess();
		// 	var processTotal = process.WorkingSet64 + process.PagedSystemMemorySize64;
		// 	return (ulong)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes - processTotal);
		// }
		
		[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
		static ulong GetFreeMemoryWindows() {
			using var counter = new PerformanceCounter("Memory", "Available Bytes");
			return Convert.ToUInt64(counter.NextValue());
		}

		static ulong GetFreeMemoryMac() {
			var output = ExecuteBashCommand("vm_stat | grep 'Pages free:' | awk '{print $3}'");
			var value  = Convert.ToUInt64(output.TrimEnd('.')) * 4096; // Pages are 4KB in size
			
			return value;
		}

		static ulong GetFreeMemoryLinux() {
			var output = ExecuteBashCommand("free -b | grep Mem | awk '{print $4}'");
			return Convert.ToUInt64(output);
		}
	}
	
	public static (double OneMinute, double FiveMinutes, double FifteenMinutes) GetLoadAverages() {
		return OsPlatform switch {
			RuntimeOSPlatform.Linux => GetLoadAveragesLinux(),
			RuntimeOSPlatform.OSX   => GetLoadAveragesMac(),
			_                       => throw new NotSupportedException("Operating system not supported")
		};

		static (double OneMinute, double FiveMinutes, double FifteenMinutes) GetLoadAveragesMac() {
			// example: 14:49  up 37 days,  3:27, 2 users, load averages: 1.57 2.09 2.26

			var output = ExecuteBashCommand("uptime");
			var values = output[(output.LastIndexOf("load averages:", StringComparison.OrdinalIgnoreCase) + 14)..]
				.Split(' ', StringSplitOptions.RemoveEmptyEntries);

			return (
				OneMinute: Convert.ToDouble(values[0], CultureInfo.InvariantCulture),
				FiveMinutes: Convert.ToDouble(values[1], CultureInfo.InvariantCulture),
				FifteenMinutes: Convert.ToDouble(values[2], CultureInfo.InvariantCulture)
			);
		}
	
		static (double OneMinute, double FiveMinutes, double FifteenMinutes) GetLoadAveragesLinux() {
			var output = ExecuteBashCommand("uptime");
			var values = output[(output.IndexOf("load average:", StringComparison.OrdinalIgnoreCase) + 13)..].Split(',');

			return (
				OneMinute: Convert.ToDouble(values[0], CultureInfo.InvariantCulture),
				FiveMinutes: Convert.ToDouble(values[1], CultureInfo.InvariantCulture),
				FifteenMinutes: Convert.ToDouble(values[2], CultureInfo.InvariantCulture)
			);
		}
	}
	
	public static (float Usage, float IdleTime, float TotalTime) GetCpuStats() {
		return OsPlatform switch {
			RuntimeOSPlatform.Linux   => GetCpuStatsLinux(),
			RuntimeOSPlatform.OSX     => GetCpuStatsMacOs(),
			RuntimeOSPlatform.Windows => GetCpuStatsWindows(),
			_                         => throw new NotSupportedException("Operating system not supported")
		};
		
		[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
		static (float Usage, float IdleTime, float TotalTime) GetCpuStatsWindows() {
			using var counter  = new PerformanceCounter("Processor", "% Processor Time", "_Total");

			var cpuUsage = counter.NextValue();
			
			// Windows does not provide direct idle time and total time, so we approximate
			var idleTime  = 100 - cpuUsage;
			var totalTime = 100;
			
			return (cpuUsage, idleTime, totalTime);
		}

		static (float Usage, float IdleTime, float TotalTime) GetCpuStatsMacOs() {
			var output = ExecuteBashCommand("iostat -c 2 -w 1");
			
			// The second last line contains the CPU stats
			var cpuStats = output.Split(Environment.NewLine)[^2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
			
			var userTime   = Convert.ToSingle(cpuStats[0], CultureInfo.InvariantCulture);
			var systemTime = Convert.ToSingle(cpuStats[1], CultureInfo.InvariantCulture);
			var idleTime   = Convert.ToSingle(cpuStats[2], CultureInfo.InvariantCulture);

			var totalTime = userTime + systemTime + idleTime;
			var usage     = 100.0f * (totalTime - idleTime) / totalTime;
			
			return (usage, idleTime, totalTime);
			
			// static float GetCpuUsageMac() {
			// 	var output = ExecuteBashCommand("ps -A -o %cpu");
			//
			// 	float total  = 0;
			// 	// Skip the header
			// 	foreach (var line in output.Split(Environment.NewLine).Skip(1))
			// 		if (float.TryParse(line, out var cpuUsage)) total += cpuUsage;
			//
			// 	return total;
			// }
		}

		static (float Usage, float IdleTime, float TotalTime) GetCpuStatsLinux() {
	        var output = ExecuteBashCommand("cat /proc/stat | grep '^cpu '");
	        
	        var cpuTimes  = output.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(long.Parse).ToList();
	        var idleTime  = cpuTimes[3]; // idle time is 4th item
	        var totalTime = cpuTimes.Sum();
	        var usage     = 100.0f * (totalTime - idleTime) / totalTime;
	        
	        return (usage, idleTime, totalTime);
        }
	}
	
	static string ExecuteBashCommand(string command) {
		var psi = new ProcessStartInfo {
			FileName               = "/bin/bash",
			RedirectStandardInput  = true,
			RedirectStandardOutput = true,
			UseShellExecute        = false
		};

		using var process = Process.Start(psi);

		if (process == null)
			throw new InvalidOperationException("Could not start bash process to retrieve system runtime stats");

		process.StandardInput.WriteLine(command);
		process.StandardInput.Flush();
		process.StandardInput.Close();

		return process.StandardOutput.ReadToEnd().Trim();
	}
}