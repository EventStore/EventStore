using System;
using System.Net;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Embedded;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.ClientAPI.Helpers {
	public static class TestConnection {
		private static int _nextConnId = -1;

		public static IEventStoreConnection Create(IPEndPoint endPoint, TcpType tcpType = TcpType.Normal,
			UserCredentials userCredentials = null) {
			return EventStoreConnection.Create(Settings(tcpType, userCredentials),
				endPoint.ToESTcpUri(),
				string.Format("ESC-{0}", Interlocked.Increment(ref _nextConnId)));
		}

		public static IEventStoreConnection To(MiniNode miniNode, TcpType tcpType,
			UserCredentials userCredentials = null) {
			return EventStoreConnection.Create(Settings(tcpType, userCredentials),
				tcpType == TcpType.Ssl ? miniNode.TcpSecEndPoint.ToESTcpUri() : miniNode.TcpEndPoint.ToESTcpUri(),
				string.Format("ESC-{0}", Interlocked.Increment(ref _nextConnId)));
		}

		private static ConnectionSettingsBuilder Settings(TcpType tcpType, UserCredentials userCredentials) {
			var settings = ConnectionSettings.Create()
				.SetDefaultUserCredentials(userCredentials)
				.UseCustomLogger(ClientApiLoggerBridge.Default)
				.EnableVerboseLogging()
				.LimitReconnectionsTo(10)
				.LimitRetriesForOperationTo(100)
				.SetTimeoutCheckPeriodTo(TimeSpan.FromMilliseconds(100))
				.SetReconnectionDelayTo(TimeSpan.Zero)
				.FailOnNoServerResponse()
				.SetOperationTimeoutTo(TimeSpan.FromDays(1));
			if (tcpType == TcpType.Ssl)
				settings.UseSslConnection("ES", false);
			return settings;
		}
	}

	public static class EmbeddedTestConnection {
		private static int _nextConnId = -1;

		public static IEventStoreConnection To(MiniNode miniNode, UserCredentials credentials = null) {
			return EmbeddedEventStoreConnection.Create(miniNode.Node, Settings(credentials),
				string.Format("ESC-{0}", Interlocked.Increment(ref _nextConnId)));
		}

		private static ConnectionSettingsBuilder Settings(UserCredentials credentials = null) {
			var settings = ConnectionSettings.Create()
				.SetDefaultUserCredentials(credentials)
				.UseCustomLogger(ClientApiLoggerBridge.Default)
				.EnableVerboseLogging()
				.LimitReconnectionsTo(10)
				.LimitRetriesForOperationTo(100)
				.SetTimeoutCheckPeriodTo(TimeSpan.FromMilliseconds(100))
				.SetReconnectionDelayTo(TimeSpan.Zero)
				.FailOnNoServerResponse()
				.SetOperationTimeoutTo(TimeSpan.FromDays(1));
			return settings;
		}
	}
}
