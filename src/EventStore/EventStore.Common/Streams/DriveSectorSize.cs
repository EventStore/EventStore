using System.IO;

namespace EventStore.Common.Streams
{
#if ! __MonoCS__
    public class DriveSectorSize
    {
        /// <summary>
        /// Return the sector size of the volume the specified filepath lives on.
        /// </summary>
        /// <param name="path">UNC path name for the file or directory</param>
        /// <returns>device sector size in bytes </returns>
        public static uint GetDriveSectorSize(string path)
        {
            uint size = 512; // sector size in bytes. 
            uint toss;       // ignored outputs
            WinApi.GetDiskFreeSpace(Path.GetPathRoot(path), out toss, out size, out toss, out toss);
            return size;
        }
    }
#endif

    class A {}//at least one thing needs to be in the namespace or else mono will not emit it
}