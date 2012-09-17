using System;
using System.IO;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog
{
    public class VersionedPatternFileNamingStrategy : IFileNamingStrategy
    {
        private readonly string _path;
        private readonly string _prefix;

        public VersionedPatternFileNamingStrategy(string path, string prefix)
        {
            Ensure.NotNull(path, "path");
            Ensure.NotNull(prefix, "prefix");
            _path = path;
            _prefix = prefix;
        }

        public string GetFilenameFor(int index, int version = 0)
        {
            Ensure.Nonnegative(index, "index");
            Ensure.Nonnegative(version, "version");

            return Path.Combine(_path, string.Format("{0}{1:000000}.{2:000000}", _prefix, index, version));
        }

        public string[] GetAllVersionsFor(int index)
        {
            var versions = Directory.GetFiles(_path, string.Format("{0}{1:000000}.*", _prefix, index));
            Array.Sort(versions, StringComparer.CurrentCultureIgnoreCase);
            Array.Reverse(versions);
            return versions;
        }

        public string[] GetAllPresentFiles()
        {
            return Directory.GetFiles(_path, string.Format("{0}*.*", _prefix));
        }
    }
}