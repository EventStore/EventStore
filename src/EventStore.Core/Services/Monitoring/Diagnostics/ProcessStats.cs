using System.IO;
using System.Runtime;
using System.Threading.Tasks;
using OsxNative = System.Diagnostics.Interop.OsxNative;
using WindowsNative = System.Diagnostics.Interop.WindowsNative;

namespace System.Diagnostics;

public static class ProcessStats {
    public static ValueTask<DiskIoData> GetDiskIo(int processId) {
        return RuntimeInformation.OsPlatform switch {
            RuntimeOSPlatform.Linux   => GetDiskIoLinux(processId),
            RuntimeOSPlatform.OSX     => GetDiskIoOsx(processId),
            RuntimeOSPlatform.Windows => GetDiskIoWindows(processId),
            _                         => throw new NotSupportedException("Operating system not supported")
        };

        static async ValueTask<DiskIoData> GetDiskIoLinux(int processId) {
            var procIoFile = $"/proc/{processId}/io";

            try {
                if (!File.Exists(procIoFile))
                    throw new FileNotFoundException("Process I/O info file does not exist.");

                var result = new DiskIoData();

                await foreach (var line in File.ReadLinesAsync(procIoFile)) {
                    if (TryExtractIoValue(line, "read_bytes", out var readBytes))
                        result = result with { ReadBytes = readBytes };
                    else if (TryExtractIoValue(line, "write_bytes", out var writeBytes))
                        result = result with { WrittenBytes = writeBytes };
                    else if (TryExtractIoValue(line, "syscr", out var readOps))
                        result = result with { ReadOps = readOps };
                    else if (TryExtractIoValue(line, "syscw", out var writeOps)) {
                        result = result with { WriteOps = writeOps };
                        break;
                    }
                }

                return result;
            }
            catch (Exception ex) {
                throw new ApplicationException("Failed to read process I/O info.", ex);
            }

            static bool TryExtractIoValue(string line, string key, out ulong value) {
                if (line.StartsWith(key)) {
                    var rawValue = line[(key.Length + 1)..].Trim(); // handle the `:` character
                    value = Convert.ToUInt64(rawValue);
                    return true;
                }

                value = 0;
                return false;
            }
        }

        static ValueTask<DiskIoData> GetDiskIoOsx(int processId) {
            var value = OsxNative.IO.GetDiskIo(processId);
            return ValueTask.FromResult(new DiskIoData(value.ReadBytes, value.WrittenBytes, 0, 0));
        }

        static ValueTask<DiskIoData> GetDiskIoWindows(int processId) =>
            ValueTask.FromResult(WindowsNative.IO.GetDiskIo(processId));
    }

    public static ValueTask<DiskIoData> GetDiskIo() =>
        GetDiskIo(Environment.ProcessId);

    public static DiskIoData GetDiskIoSync() =>
        GetDiskIo().AsTask().GetAwaiter().GetResult();
}

public readonly record struct DiskIoData {
    public DiskIoData() { }

    public DiskIoData(ulong readBytes, ulong writtenBytes, ulong readOps, ulong writeOps) {
        ReadBytes    = readBytes;
        WrittenBytes = writtenBytes;
        ReadOps      = readOps;
        WriteOps     = writeOps;
    }

    public ulong ReadBytes    { get; init; }
    public ulong WrittenBytes { get; init; }
    public ulong ReadOps      { get; init; }
    public ulong WriteOps     { get; init; }
}