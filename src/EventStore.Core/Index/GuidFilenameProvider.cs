using System;
using System.IO;

namespace EventStore.Core.Index {
	public class GuidFilenameProvider : IIndexFilenameProvider {
		private readonly string _directory;

		public GuidFilenameProvider(string directory) {
			_directory = directory;
		}

		public string GetFilenameNewTable() {
			return Path.Combine(_directory, Guid.NewGuid().ToString());
		}
	}
}
