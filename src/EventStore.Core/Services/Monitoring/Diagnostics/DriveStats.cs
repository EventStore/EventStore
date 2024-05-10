using System.IO;

namespace System.Diagnostics;

public static class DriveStats {
    public static DriveInfoData GetDriveInfo(string path) {
        var info = new DriveInfo(Directory.GetDirectoryRoot(path));
        var data = new DriveInfoData(info.Name, info.TotalSize, info.AvailableFreeSpace);
        return data;
    }
}

public readonly record struct DriveInfoData(string DiskName, long TotalBytes, long AvailableBytes) {
    ///<summary>
    /// Total bytes of space used by Event Store
    ///</summary>
    public long UsedBytes { get; } = TotalBytes - AvailableBytes;

    /// <summary>
    /// Percentage usage of space used by Event Store
    /// </summary>
    public int Usage { get; } = (int)(TotalBytes != 0 ? (TotalBytes - AvailableBytes) * 100 / TotalBytes : 0);
}