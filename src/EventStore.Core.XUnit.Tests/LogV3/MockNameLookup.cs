// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Linq;
using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.XUnit.Tests.LogV3;

class MockNameLookup : INameLookup<StreamId> {
	private readonly Dictionary<StreamId, string> _dict;

	public MockNameLookup(Dictionary<StreamId, string> dict) {
		_dict = dict;
	}

	public bool TryGetLastValue(out StreamId last) {
		last = _dict.Count != 0 ? _dict.Keys.Max() : 0;
		return _dict.Count != 0;
	}

	public bool TryGetName(StreamId key, out string name) {
		return _dict.TryGetValue(key, out name);
	}
}
