﻿using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.Transport.Enumerators;

public abstract class ReadResponseException : Exception {
	public class StreamDeleted : ReadResponseException {
		public readonly string StreamName;

		public StreamDeleted(string streamName) {
			StreamName = streamName;
		}
	}

	public class AccessDenied : ReadResponseException { }

	public class InvalidPositionException : ReadResponseException { }

	public class Timeout : ReadResponseException {
		public readonly string ErrorMessage;

		public Timeout(string errorMessage) {
			ErrorMessage = errorMessage;
		}
	}

	public class UnknownMessage : ReadResponseException {
		public readonly Type UnknownMessageType;
		public readonly Type ExpectedMessageType;

		private UnknownMessage(Type unknownMessageType, Type expectedMessageType) {
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
