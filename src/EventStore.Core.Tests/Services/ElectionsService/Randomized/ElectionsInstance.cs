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
