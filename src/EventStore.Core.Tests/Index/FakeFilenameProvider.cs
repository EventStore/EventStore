// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Index;

public class FakeFilenameProvider : IIndexFilenameProvider {
	private List<string> _filenames = new List<string>();
	private int _current;


	public FakeFilenameProvider(params string[] fakenames) {
		_filenames.AddRange(fakenames);
	}

	public string GetFilenameNewTable() {
		var ret = _filenames[_current];
		_current++;
		return ret;
	}
}
