// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Infrastructure {
	public class RandTestQueueItem {
		public readonly int LogicalTime;
		public readonly int GlobalId;
		public readonly EndPoint EndPoint;
		public readonly Message Message;
		public readonly IPublisher Bus;

		public RandTestQueueItem(int logicalTime, int globalId, EndPoint endPoint, Message message, IPublisher bus) {
			LogicalTime = logicalTime;
			GlobalId = globalId;
			EndPoint = endPoint;
			Message = message;
			Bus = bus;
		}

		public override string ToString() {
			return string.Format("{0}-{1} :{2} to {3}", LogicalTime, GlobalId, Message, EndPoint.GetPort());
		}
	}
}
