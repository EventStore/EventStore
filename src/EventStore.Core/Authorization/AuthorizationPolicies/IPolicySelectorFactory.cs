// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Core.Bus;

namespace EventStore.Core.Authorization.AuthorizationPolicies;

// TODO: Move this to EventStore.Plugins when we're decoupling the StreamPolicies Plugin
public interface IPolicySelectorFactory {
	string CommandLineName { get; }
	IPolicySelector Create(IPublisher publisher);
	Task<bool> Enable();
	Task Disable();
}
