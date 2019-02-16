using System;
using NUnit.Framework;
using EventStore.ClientAPI;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Util;
using System.IO;

namespace EventStore.Projections.Core.Tests.ClientAPI.projectionsManager {
	class ProjectionsManagerTestSuiteMarkerBase {
		public static MiniNode Node;
		public static IEventStoreConnection Connection;
		public static string PathName;
		public static int Counter;

		[OneTimeSetUp]
		public void SetUp() {
			var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
			PathName = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}", Guid.NewGuid(), typeName));
			Directory.CreateDirectory(PathName);

			Counter = 0;
			CreateNode();

			Connection = TestConnection.Create(Node.TcpEndPoint);
			Connection.ConnectAsync().Wait();
		}

		[OneTimeTearDown]
		public void TearDown() {
			Connection.Close();
			Node.Shutdown();
			Connection = null;
			Node = null;

			Directory.Delete(PathName, true);
		}

		protected void CreateNode() {
			var projections = new ProjectionsSubsystem(1, runProjections: ProjectionType.All,
				startStandardProjections: false,
				projectionQueryExpiry: TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault),
				faultOutOfOrderProjections: Opts.FaultOutOfOrderProjectionsDefault);
			Node = new MiniNode(
				PathName, inMemDb: true, skipInitializeStandardUsersCheck: false,
				subsystems: new ISubsystem[] {projections});
			Node.Start();
		}
	}

	class SetUpFixture : ProjectionsManagerTestSuiteMarkerBase {
	}
}
