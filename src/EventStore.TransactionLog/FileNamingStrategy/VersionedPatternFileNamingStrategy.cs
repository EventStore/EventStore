using System;
using System.IO;
using System.Text.RegularExpressions;
using EventStore.Common.Utils;
using System.Linq;

namespace EventStore.Core.TransactionLog.FileNamingStrategy {
	public class VersionedPatternFileNamingStrategy : IFileNamingStrategy {
		private readonly string _path;
		private readonly string _prefix;
		private readonly Regex _chunkNamePattern;

		public VersionedPatternFileNamingStrategy(string path, string prefix) {
			Ensure.NotNull(path, "path");
			Ensure.NotNull(prefix, "prefix");
			_path = path;
			_prefix = prefix;

			_chunkNamePattern = new Regex("^" + _prefix + @"\d{6}\.\w{6}$");
		}

		public string GetFilenameFor(int index, int version) {
			Ensure.Nonnegative(index, "index");
			Ensure.Nonnegative(version, "version");

			return Path.Combine(_path, string.Format("{0}{1:000000}.{2:000000}", _prefix, index, version));
		}

		public string DetermineBestVersionFilenameFor(int index) {
			var allVersions = GetAllVersionsFor(index);
			if (allVersions.Length == 0)
				return GetFilenameFor(index, 0);
			int lastVersion;
			if (!int.TryParse(allVersions[0].Substring(allVersions[0].LastIndexOf('.') + 1), out lastVersion))
				throw new Exception(string.Format("Could not determine version from filename '{0}'.", allVersions[0]));
			return GetFilenameFor(index, lastVersion + 1);
		}

		public string[] GetAllVersionsFor(int index) {
			var versions = Directory.EnumerateFiles(_path, string.Format("{0}{1:000000}.*", _prefix, index))
				.Where(x => _chunkNamePattern.IsMatch(Path.GetFileName(x)))
				.OrderByDescending(x => x, StringComparer.CurrentCultureIgnoreCase)
				.ToArray();
			return versions;
		}

		public string[] GetAllPresentFiles() {
			var versions = Directory.EnumerateFiles(_path, string.Format("{0}*.*", _prefix))
				.Where(x => _chunkNamePattern.IsMatch(Path.GetFileName(x)))
				.ToArray();
			return versions;
		}

		public string GetTempFilename() {
			return Path.Combine(_path, string.Format("{0}.tmp", Guid.NewGuid()));
		}

		public string[] GetAllTempFiles() {
			return Directory.GetFiles(_path, "*.tmp");
		}
	}
}
