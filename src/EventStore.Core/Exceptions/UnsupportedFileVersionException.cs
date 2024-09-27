using System;

namespace EventStore.Core.Exceptions {
	public class UnsupportedFileVersionException : Exception {
		public UnsupportedFileVersionException(string filename, byte fileVersion, byte lastSupportedVersion,
			string message = "")
			: base(string.Format("File {0} has unsupported version: {1}. The latest supported version is: {2}. {3}",
				filename,
				fileVersion,
				lastSupportedVersion,
				message)) {
		}
	}
}
