using System;
using System.Diagnostics;
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

		private int GetFileNumberFor(string path) {
			var fileName = Path.GetFileName(path);
			if (!_chunkNamePattern.IsMatch(fileName))
				throw new ArgumentException($"Invalid file name: {fileName}");

			var start = _prefix.Length;
			var end = fileName.IndexOf('.', _prefix.Length);
			Debug.Assert(end != -1);

			if (!int.TryParse(fileName[start..end], out var chunkNumber))
				throw new ArgumentException($"Invalid file name: {fileName}");

			return chunkNumber;
		}

		private int GetFileVersionFor(string path) {
			var fileName = Path.GetFileName(path);
			if (!_chunkNamePattern.IsMatch(fileName))
				throw new ArgumentException($"Invalid file name: {fileName}");

			var dot = fileName.IndexOf('.', _prefix.Length);
			Debug.Assert(dot != -1);

			if (!int.TryParse(fileName[(dot+1)..], out var chunkVersion))
				throw new ArgumentException($"Invalid file name: {fileName}");

			return chunkVersion;
		}

		public void EnumerateAllFiles(
			Func<string, int, int, int> getNextFileNumber,
			Action<string, int, int> onLatestVersionFound = null,
			Action<string, int> onOldVersionFound = null,
			Action<string, int> onFileMissing = null) {
			var allFiles = GetAllPresentFiles();
			Array.Sort(allFiles, StringComparer.CurrentCultureIgnoreCase);

			int expectedChunkNumber = 0;
			for (int i = 0; i < allFiles.Length; i++) {
				var chunkFileName = allFiles[i];
				var chunkNumber = GetFileNumberFor(allFiles[i]);
				var nextChunkNumber = -1;
				if (i + 1 < allFiles.Length)
					nextChunkNumber = GetFileNumberFor(allFiles[i+1]);

				if (chunkNumber < expectedChunkNumber) { // present in an earlier, merged, chunk
					onOldVersionFound?.Invoke(chunkFileName, chunkNumber);
					continue;
				}

				if (chunkNumber > expectedChunkNumber) { // one or more chunks are missing
					for (int j = expectedChunkNumber; j < chunkNumber; j++) {
						onFileMissing?.Invoke(GetFilenameFor(j, 0), j);
					}
					// set the expected chunk number to prevent calling onFileMissing() again for the same chunk numbers
					expectedChunkNumber = chunkNumber;
				}

				if (chunkNumber == nextChunkNumber) { // there is a newer version of this chunk
					onOldVersionFound?.Invoke(chunkFileName, chunkNumber);
				} else { // latest version of chunk with the expected chunk number
					expectedChunkNumber = getNextFileNumber(chunkFileName, chunkNumber, GetFileVersionFor(chunkFileName));
					onLatestVersionFound?.Invoke(chunkFileName, chunkNumber, expectedChunkNumber - 1);
				}
			}
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
