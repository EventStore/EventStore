// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static partial class GrpcMessage {
		[DerivedMessage(CoreMessage.Grpc)]
		public partial class SendOverGrpc : Message {

			public readonly EndPoint DestinationEndpoint;
			public readonly Message Message;
			public readonly DateTime LiveUntil;

			public SendOverGrpc(EndPoint destinationEndpoint, Message message, DateTime liveUntil) {
				DestinationEndpoint = destinationEndpoint;
				Message = message;
				LiveUntil = liveUntil;
			}
		}
	}
}
