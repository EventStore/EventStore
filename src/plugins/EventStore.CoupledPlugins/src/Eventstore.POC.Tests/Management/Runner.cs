// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace Eventstore.POC.Tests.Management;
using EventStore.POC.ConnectorsEngine.Infrastructure;

public class Runner<TSut>(TSut Sut) where TSut : Aggregate {
	public void Run(TestDefinition<TSut> def) {
		// given
		foreach (var x in def.Givens)
			Sut.BaseState.Apply(x);

		// when
		foreach (var x in def.Whens)
			x(Sut);

		// then
		Assert.Equal(
			def.Thens,
			Sut.BaseState.Pending);

		// giving
		if (def.CheckGivings)
			Assert.Equal(
				def.Givings,
				[
					.. def.Givens,
					.. def.Thens,
				]);
	}
}
