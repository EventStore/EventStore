// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable
using System;
using System.Threading.Tasks;
using DotNext.Threading;
using EventStore.Core.Authorization;
using EventStore.Core.Authorization.AuthorizationPolicies;
using EventStore.Core.Bus;

namespace EventStore.Core.XUnit.Tests.Authorization;

public class FakePolicySelectorPlugin : IPolicySelectorFactory {
	private readonly string _name;
	private readonly bool _canBeEnabled;
	public readonly AsyncManualResetEvent OnEnabled;
	public readonly AsyncManualResetEvent OnDisabled;
	public string CommandLineName => _name;

	public FakePolicySelectorPlugin(string name, bool canBeEnabled) {
		_name = name;
		_canBeEnabled = canBeEnabled;
		OnEnabled = new AsyncManualResetEvent(false);
		OnDisabled = new AsyncManualResetEvent(false);
	}
	public IPolicySelector Create(IPublisher publisher) {
		return new FakePolicySelector(_name);
	}

	public Task<bool> Enable() {
		OnEnabled.Set();
		return Task.FromResult(_canBeEnabled);
	}

	public Task Disable() {
		OnDisabled.Set();
		return Task.CompletedTask;
	}
}
public class FakePolicySelector : IPolicySelector {
	private readonly string _name;
	public FakePolicySelector(string name) {
		_name = name;
	}
	public ReadOnlyPolicy Select() {
		return new Policy(_name, 1, DateTimeOffset.MaxValue).AsReadOnly();
	}
}
