// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
