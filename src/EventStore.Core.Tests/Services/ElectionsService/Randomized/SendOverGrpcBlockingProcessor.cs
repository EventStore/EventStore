// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Infrastructure;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized;

internal class SendOverGrpcBlockingProcessor : SendOverGrpcProcessor {
	private readonly Dictionary<EndPoint, bool> _endpointsToSkip;

	public SendOverGrpcBlockingProcessor(Random rnd,
		RandomTestRunner runner,
		double lossProb,
		double dupProb,
		int maxDelay) : base(rnd, runner, lossProb, dupProb, maxDelay) {
		_endpointsToSkip = new Dictionary<EndPoint, bool>();
	}

	public void RegisterEndpointToSkip(EndPoint endPoint, bool shouldSkip) {
		_endpointsToSkip[endPoint] = shouldSkip;
	}

	protected override bool ShouldSkipMessage(GrpcMessage.SendOverGrpc message) {
		bool shouldSkip;
		return _endpointsToSkip.TryGetValue(message.DestinationEndpoint, out shouldSkip) && shouldSkip;
	}
}
