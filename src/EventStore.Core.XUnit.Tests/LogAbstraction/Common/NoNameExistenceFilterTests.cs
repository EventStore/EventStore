// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogAbstraction;
using EventStore.Core.LogAbstraction.Common;

namespace EventStore.Core.XUnit.Tests.LogAbstraction.Common;

public class NoNameExistenceFilterTests : INameExistenceFilterTests {
	protected override INameExistenceFilter Sut { get; set; } =
		new NoNameExistenceFilter();
}
