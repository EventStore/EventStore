using System;

namespace EventStore.Core.Exceptions {
	public class UnsupportedFileVersionException : Exception {
		public UnsupportedFileVersionException(string filename, byte fileVersion, byte lastSupportedVersion)
			: base(string.Format("File {0} has unsupported version: {1}. The last supported version is: {2}.",
				filename,
				fileVersion,
				lastSupportedVersion)) {
		}
	}
}
