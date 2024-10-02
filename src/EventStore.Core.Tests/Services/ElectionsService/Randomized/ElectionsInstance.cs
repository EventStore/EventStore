// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net;
using EventStore.Core.Bus;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized {
	internal class ElectionsInstance {
		public readonly Guid InstanceId;
		public readonly EndPoint EndPoint;

		public readonly IPublisher InputBus;
		public readonly IPublisher OutputBus;

		public ElectionsInstance(Guid instanceId, EndPoint endPoint, IPublisher inputBus, IPublisher outputBus) {
			InstanceId = instanceId;
			EndPoint = endPoint;
			InputBus = inputBus;
			OutputBus = outputBus;
		}
	}
}
