using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Interop;
using System.Runtime;
using System.Threading.Tasks;
using static System.Convert;
using static System.Globalization.CultureInfo;
using static System.Reflection.BindingFlags;

namespace System.Diagnostics;

public static class RuntimeStats {
    static RuntimeStats() {
        GetCpuUsageInternal = typeof(GC)
            .Assembly
            .GetType("System.Diagnostics.Tracing.RuntimeEventSourceHelper")!
            .GetMethod("GetCpuUsage", Static | NonPublic)!
            .CreateDelegate<Func<double>>();

        GetExceptionCountInternal = typeof(Exception)
            .GetMethod("GetExceptionCount", Static | NonPublic)!
            .CreateDelegate<Func<uint>>();

        GetLastGCPercentTimeInGCInternal = typeof(GC)
            .GetMethod("GetLastGCPercentTimeInGC", Static | NonPublic)!
            .CreateDelegate<Func<int>>();
    }

    static Func<double> GetCpuUsageInternal              { get; }
    static Func<uint>   GetExceptionCountInternal        { get; }
    static Func<int>    GetLastGCPercentTimeInGCInternal { get; }

    public static double GetCpuUsage() => GetCpuUsageInternal();

    public static int GetExceptionCount() => (int)GetExceptionCountInternal();

    public static int GetLastGCPercentTimeInGC() => GetLastGCPercentTimeInGCInternal();

    public static ValueTask<long> GetTotalMemory() {
        // return GetTotalMemoryFromGC();
        //
        // static ValueTask<long> GetTotalMemoryFromGC() {
        //     var value = GC.GetGCMemoryInfo(GCKind.Background).TotalAvailableMemoryBytes;
        //     return ValueTask.FromResult(value);
        // }

        return RuntimeInformation.OsPlatform switch {
            RuntimeOSPlatform.Linux   => GetTotalMemoryLinux(),
            RuntimeOSPlatform.FreeBSD => GetTotalMemoryFreeBSD(),
            RuntimeOSPlatform.OSX     => GetTotalMemoryOSX(),
            RuntimeOSPlatform.Windows => GetTotalMemoryWindows(),
            _                         => throw new NotSupportedException("Operating system not supported")
        };

        static async ValueTask<long> GetTotalMemoryLinux() {
            var output = await ExecuteBashCommandAsync("grep MemTotal /proc/meminfo");
            var parts  = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var value  = ToInt64(parts[1]) * 1024; // Convert from KB to bytes
            return value;
        }

        static async ValueTask<long> GetTotalMemoryFreeBSD() {
            var output = await ExecuteBashCommandAsync("sysctl -n hw.physmem");
            var value  = ToInt64(output.Trim());
            return value;
        }


        static async ValueTask<long> GetTotalMemoryOSX() {
            var output = await ExecuteBashCommandAsync("sysctl -n hw.memsize");
            var value  = ToInt64(output);
            return value;
        }


        static ValueTask<long> GetTotalMemoryWindows() {
            var value = (long) WindowsNative.Memory.GetTotalMemory();
            return ValueTask.FromResult(value);
        }
    }

    public static ValueTask<long> GetFreeMemory() {
        return RuntimeInformation.OsPlatform switch {
            RuntimeOSPlatform.Linux   => GetFreeMemoryLinux(),
            RuntimeOSPlatform.FreeBSD => GetFreeMemoryLinux(),
            RuntimeOSPlatform.OSX     => GetFreeMemoryOSX(true),
            RuntimeOSPlatform.Windows => GetFreeMemoryWindows(),
            _ => throw new NotSupportedException("Operating system not supported")
        };

        static async ValueTask<long> GetFreeMemoryLinux() {
            var output = await ExecuteBashCommandAsync("grep MemFree /proc/meminfo"); // old code uses MemAvailable
            var parts  = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var value  = ToInt64(parts[1]) * 1024; // Convert KB to bytes
            return value;
        }

        static async ValueTask<long> GetFreeMemoryOSX(bool native = false) {
            if (native)
                return OsxNative.Memory.GetFreeMemory();

            var output = await ExecuteBashCommandAsync("vm_stat | head -n 2");

            var lines = output.Split('\n');
            var value = ParseFreePages(lines[1]) * ParsePageSize(lines[0]);

            return value;

            static long ParsePageSize(string line) =>
                ToInt64(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[7]);

            static long ParseFreePages(string line) =>
                ToInt64(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[2].TrimEnd('.'));
        }

        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
        static ValueTask<long> GetFreeMemoryWindows() {
            using var counter = new PerformanceCounter("Memory", "Available Bytes");
            var value = ToInt64(counter.NextValue());
            return ValueTask.FromResult(value);
        }
    }

    public static ValueTask<(double OneMinute, double FiveMinutes, double FifteenMinutes)> GetCpuLoadAverages() {
        return RuntimeInformation.OsPlatform switch {
            RuntimeOSPlatform.Linux   => GetLoadAveragesLinux(),
            RuntimeOSPlatform.FreeBSD => GetLoadAveragesFreeBSD(),
            RuntimeOSPlatform.OSX     => GetLoadAveragesMac(),
            RuntimeOSPlatform.Windows => default,
            _                         => throw new NotSupportedException("Operating system not supported")
        };

        static async ValueTask<(double OneMinute, double FiveMinutes, double FifteenMinutes)> GetLoadAveragesLinux() {
            // On Linux, the /proc/loadavg file provides load averages along with some additional scheduling information.
            // The file typically looks something like this:
            //
            // 0.01 0.05 0.05 1/789 12345
            //
            // Here:
            //
            // - 0.01 is the 1-minute load average.
            // - 0.05 is the 5-minute load average.
            // - 0.05 is the 15-minute load average.
            // - 1/789 indicates the number of currently running processes over the total number of processes.
            // - 12345 is the last process ID used.

            var output = await ExecuteBashCommandAsync("grep -Eo '^[^ ]+ [^ ]+ [^ ]+' /proc/loadavg");
            var values = output.Split(' ');

            return (
                OneMinute: ToDouble(values[0], InvariantCulture),
                FiveMinutes: ToDouble(values[1], InvariantCulture),
                FifteenMinutes: ToDouble(values[2], InvariantCulture)
            );
        }

        static async ValueTask<(double OneMinute, double FiveMinutes, double FifteenMinutes)> GetLoadAveragesMac() {
            // On macOS, the uptime command might give you something like this:
            //
            // 14:55  up 10 days,  4:02, 4 users, load averages: 2.43 2.72 2.89
            //
            // Here:
            //
            // - 2.43 is the 1-minute load average.
            // - 2.72 is the 5-minute load average.
            // - 2.89 is the 15-minute load average.

            var output       = await ExecuteBashCommandAsync("uptime");
            var startIndex   = output.LastIndexOf(':') + 1; // find the last colon and start right after it
            var loadAverages = output[startIndex..].Trim();
            var values       = loadAverages.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return (
                OneMinute: ToDouble(values[0], InvariantCulture),
                FiveMinutes: ToDouble(values[1], InvariantCulture),
                FifteenMinutes: ToDouble(values[2], InvariantCulture)
            );
        }

        static async ValueTask<(double OneMinute, double FiveMinutes, double FifteenMinutes)> GetLoadAveragesFreeBSD() {
            // works on macOS as well
            // Example output: "{ 0.12 0.26 0.21 }"
            var output = await ExecuteBashCommandAsync("sysctl -n vm.loadavg");
            var values = output.Trim('{', '}', ' ').Split(' ');

            return (
                OneMinute: ToDouble(values[0], InvariantCulture),
                FiveMinutes: ToDouble(values[1], InvariantCulture),
                FifteenMinutes: ToDouble(values[2], InvariantCulture)
            );
        }
    }

    public static long GetTotalMemorySync() =>
        GetTotalMemory().AsTask().GetAwaiter().GetResult();

    public static long GetFreeMemorySync() =>
        GetFreeMemory().AsTask().GetAwaiter().GetResult();

    public static (double OneMinute, double FiveMinutes, double FifteenMinutes) GetCpuLoadAveragesSync() =>
        GetCpuLoadAverages().AsTask().GetAwaiter().GetResult();

    static async ValueTask<string> ExecuteBashCommandAsync(string command) {
        var escapedArgs = command.Replace(@"\", @"\\");

        var psi = new ProcessStartInfo {
            FileName               = "/bin/bash",
            Arguments              = $"-c \"{escapedArgs}\"",
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var process = Process.Start(psi);

        if (process is null)
            throw new InvalidOperationException($"Could not start bash process to execute: {psi.FileName} {psi.Arguments}");

        var result = await process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();

        return result.Trim();
    }
}
