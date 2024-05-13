namespace System.Diagnostics;

public static class DriveStats {
    public static DriveData GetDriveInfo(string path) {
        var info = new DriveInfo(Directory.GetDirectoryRoot(path));
        var data = new DriveData(info.Name, info.TotalSize, info.AvailableFreeSpace);
        return data;
    }
}

/// <summary>
/// Represents a data structure for drive information.
/// </summary>
/// <param name="DiskName">The name of the disk.</param>
/// <param name="TotalBytes">The total size of the disk in bytes.</param>
/// <param name="AvailableBytes">The available free space on the disk in bytes.</param>
public readonly record struct DriveData(string DiskName, long TotalBytes, long AvailableBytes) 
{
    /// <summary>
    /// The used space on the disk in bytes.
    /// </summary>
    public long UsedBytes { get; } = TotalBytes - AvailableBytes;

    /// <summary>
    /// The usage of the disk as a percentage of the total size.
    /// </summary>
    public int Usage { get; } = (int)(TotalBytes != 0 ? (TotalBytes - AvailableBytes) * 100 / TotalBytes : 0);
}