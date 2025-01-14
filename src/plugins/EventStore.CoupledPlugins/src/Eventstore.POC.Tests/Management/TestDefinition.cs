// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace Eventstore.POC.Tests.Management;
using EventStore.POC.ConnectorsEngine.Infrastructure;

public class TestDefinition<TSut> {
	private readonly List<Message> _givens = [];
	private readonly List<Action<TSut>> _whens = [];
	private readonly List<Message> _thens = [];
	private readonly List<Message> _givings = [];

	public IReadOnlyList<Message> Givens => _givens;
	public IReadOnlyList<Action<TSut>> Whens => _whens;
	public IReadOnlyList<Message> Thens => _thens;
	public IReadOnlyList<Message> Givings => _givings;
	public bool CheckGivings { get; private set; }

	public TestDefinition<TSut> Given(params Message[] givens) {
		_givens.AddRange(givens);
		return this;
	}

	public TestDefinition<TSut> Given(Func<Message[]> f) {
		try {
			return Given(f());
		} catch {
			Assert.Fail("Error in Given action");
			return this;
		}
	}

	public TestDefinition<TSut> When(Action<TSut> f) {
		_whens.Add(f);
		return this;
	}

	public TestDefinition<TSut> Then(params Message[] expecteds) {
		_thens.AddRange(expecteds);
		return this;
	}

	public TestDefinition<TSut> Giving(params Message[] givings) {
		CheckGivings = true;
		_givings.AddRange(givings);
		return this;
	}
}
