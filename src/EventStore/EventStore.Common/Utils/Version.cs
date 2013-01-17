using System;
using System.Diagnostics;
using System.Reflection;

namespace EventStore.Common.Utils
{
    public static class VersionInfo
    {
        public static readonly string AssemblyVersion;
        public static readonly string FileVersion;
        public static readonly string ProductVersion;

        public static readonly string Version;
        public static readonly string Branch;
        public static readonly string Hashtag;
        public static readonly string Timestamp;

        static VersionInfo()
        {
            var assembly = Assembly.GetAssembly(typeof (VersionInfo));
            var location = assembly.Location;

            AssemblyVersion = assembly.GetName().Version.ToString();
            FileVersion = FileVersionInfo.GetVersionInfo(location).FileVersion;
            ProductVersion = FileVersionInfo.GetVersionInfo(location).ProductVersion;

            var pointIndex = ProductVersion.LastIndexOf('.');
            Version = ProductVersion.Substring(0, pointIndex);

            var parts = ProductVersion.Substring(pointIndex + 1).Split(new[] { '@' }, StringSplitOptions.RemoveEmptyEntries);
            Branch = parts[0];
            Hashtag = parts[1];
            Timestamp = parts[2];
        }
    }
}
