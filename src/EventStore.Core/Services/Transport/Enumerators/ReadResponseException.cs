// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Common;

namespace EventStore.Core.Services.Transport.Enumerators;

public abstract class ReadResponseException : Exception {
	public class StreamNotFound(string streamName) : ReadResponseException {
		public string StreamName { get; } = streamName;
	}

	public class WrongExpectedRevision(string stream, long expectedRevision, long actualRevision) : ReadResponseException {
		public string Stream { get; } = stream;

		public StreamRevision ExpectedStreamRevision { get; } = StreamRevision.FromInt64(expectedRevision);

		public StreamRevision ActualStreamRevision { get; } = StreamRevision.FromInt64(actualRevision);
	}

	public class StreamDeleted : ReadResponseException {
		public readonly string StreamName;

		public StreamDeleted(string streamName) {
			StreamName = streamName;
		}
	}

	public class AccessDenied : ReadResponseException { }

	public class InvalidPosition : ReadResponseException { }

	public class Timeout : ReadResponseException {
		public readonly string ErrorMessage;

		public Timeout(string errorMessage) {
			ErrorMessage = errorMessage;
		}
	}

	public class UnknownMessage : ReadResponseException {
		public readonly Type UnknownMessageType;
		public readonly Type ExpectedMessageType;

		public UnknownMessage(Type unknownMessageType, Type expectedMessageType) {
			UnknownMessageType = unknownMessageType;
			ExpectedMessageType = expectedMessageType;
		}

		public static UnknownMessage Create<T>(Message message) where T : Message => new(message.GetType(), typeof(T));
	}

	public class UnknownError : ReadResponseException {
		public readonly Type ResultType;
		public readonly object Result;

		public UnknownError(Type resultType, object result) {
			ResultType = resultType;
			Result = result;
		}

		public static UnknownError Create<T>(T result) => new(typeof(T), result);
	}

	public abstract class NotHandled {
		public class ServerNotReady : ReadResponseException { }

		public class ServerBusy : ReadResponseException { }

		public class LeaderInfo : ReadResponseException {
			public string Host { get; }
			public int Port { get; }

			public LeaderInfo(string host, int port) {
				Host = host;
				Port = port;
			}
		}

		public class NoLeaderInfo : ReadResponseException { }
	}
}
