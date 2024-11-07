// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.XUnit.Tests.LogV3;

class MockNameLookup : INameLookup<StreamId> {
	private readonly Dictionary<StreamId, string> _dict;

	public MockNameLookup(Dictionary<StreamId, string> dict) {
		_dict = dict;
	}

	public ValueTask<Optional<StreamId>> TryGetLastValue(CancellationToken token) {
		return new(_dict.Count > 0
			? _dict.Keys.Max()
			: Optional.None<StreamId>());
	}

	public ValueTask<string> LookupName(StreamId key, CancellationToken token) {
		return new(_dict.TryGetValue(key, out var name)
			? name
			: null);
	}
}
