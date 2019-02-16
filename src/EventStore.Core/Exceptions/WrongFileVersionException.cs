using System;

namespace EventStore.Core.Exceptions {
	public class WrongFileVersionException : Exception {
		public WrongFileVersionException(string filename, byte fileVersion, byte expectedVersion)
			: base(string.Format("File {0} has wrong version: {1}, while expected version is: {2}.",
				filename,
				fileVersion,
				expectedVersion)) {
		}
	}
}
