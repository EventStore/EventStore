using System;
using System.Text;
using NUnit.Framework;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Projections;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.SystemData;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Tests;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Services;
using EventStore.Core.Util;

namespace EventStore.Projections.Core.Tests.ClientAPI.projectionsManager {
	public abstract class SpecificationWithNodeAndProjectionsManager : SpecificationWithDirectoryPerTestFixture {
		protected MiniNode _node;
		protected ProjectionsManager _projManager;
		protected IEventStoreConnection _connection;
		protected UserCredentials _credentials;
		protected TimeSpan _timeout;
		protected string _tag;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_credentials = new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword);
			var createdMiniNode = false;
			_timeout = TimeSpan.FromSeconds(10);
			// Check if a node is running in ProjectionsManagerTestSuiteMarkerBase
			if (SetUpFixture.Connection != null && SetUpFixture.Node != null) {
				_tag = "_" + (++SetUpFixture.Counter);
				_node = SetUpFixture.Node;
				_connection = SetUpFixture.Connection;
			} else {
				createdMiniNode = true;
				_tag = "_1";

				_node = CreateNode();
				_node.Start();

				_connection = TestConnection.Create(_node.TcpEndPoint);
				_connection.ConnectAsync().Wait();
			}

			try {
				_projManager = new ProjectionsManager(new ConsoleLogger(), _node.ExtHttpEndPoint, _timeout);
				Given();
				When();
			} catch {
				if (createdMiniNode) {
					if (_connection != null) {
						try {
							_connection.Close();
						} catch {
						}
					}

					if (_node != null) {
						try {
							_node.Shutdown();
						} catch {
						}
					}
				}

				throw;
			}
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			if (SetUpFixture.Connection == null || SetUpFixture.Node == null) {
				_connection.Close();
				_node.Shutdown();
			}

			base.TestFixtureTearDown();
		}

		public abstract void Given();
		public abstract void When();

		protected MiniNode CreateNode() {
			var projections = new ProjectionsSubsystem(1, runProjections: ProjectionType.All,
				startStandardProjections: false,
				projectionQueryExpiry: TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault),
				faultOutOfOrderProjections: Opts.FaultOutOfOrderProjectionsDefault);
			return new MiniNode(
				PathName, inMemDb: true, skipInitializeStandardUsersCheck: false,
				subsystems: new ISubsystem[] {projections});
		}

		protected EventData CreateEvent(string eventType, string data) {
			return new EventData(Guid.NewGuid(), eventType, true, Encoding.UTF8.GetBytes(data), null);
		}

		protected void PostEvent(string stream, string eventType, string data) {
			_connection.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] {CreateEvent(eventType, data)}).Wait();
		}

		protected void CreateOneTimeProjection() {
			var query = CreateStandardQuery(Guid.NewGuid().ToString());
			_projManager.CreateOneTimeAsync(query, _credentials).Wait();
		}

		protected void CreateContinuousProjection(string projectionName) {
			var query = CreateStandardQuery(Guid.NewGuid().ToString());
			_projManager.CreateContinuousAsync(projectionName, query, _credentials).Wait();
		}

		protected string CreateStandardQuery(string stream) {
			return @"fromStream(""" + stream + @""")
                .when({
                    ""$any"":function(s,e) {
                        s.count = 1;
                        return s;
                    }
            });";
		}

		protected string CreateEmittingQuery(string stream, string emittingStream) {
			return @"fromStream(""" + stream + @""")
                .when({
                    ""$any"":function(s,e) {
                        emit(""" + emittingStream + @""", ""emittedEvent"", e);
                    } 
                });";
		}
	}
}
