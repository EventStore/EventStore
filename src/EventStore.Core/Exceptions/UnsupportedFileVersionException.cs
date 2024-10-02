// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
