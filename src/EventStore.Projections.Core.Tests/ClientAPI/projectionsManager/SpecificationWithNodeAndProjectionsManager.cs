using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Projections;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.SystemData;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Tests;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Services;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.ClientAPI.projectionsManager {
	public abstract class SpecificationWithNodeAndProjectionsManager : SpecificationWithDirectoryPerTestFixture {
		protected MiniNode _node;
		protected ProjectionsManager _projManager;
		protected IEventStoreConnection _connection;
		protected UserCredentials _credentials;
		protected TimeSpan _timeout;
		protected string _tag;
		private CountdownEvent _systemProjectionsCreated;
		private ProjectionsSubsystem _projectionsSubsystem;

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
				
				// System projections are created and put into the stopped state when they're ready.
				// NOTE: If this specification is changed to allow setting the 'startStandardProjections' option,
				// this will need to be updated to take that into account.
				_systemProjectionsCreated = new CountdownEvent(_systemProjections.Count()); 
				_projectionsSubsystem.MasterMainBus.Subscribe(new AdHocHandler<CoreProjectionStatusMessage.Stopped>(msg => {
					if (_systemProjections.Contains(msg.Name)
					    && _systemProjectionsCreated.CurrentCount > 0)
							_systemProjectionsCreated.Signal();
				}));
				
				_node.Start();

				if(!_systemProjectionsCreated.Wait(TimeSpan.FromSeconds(10)))
					Assert.Fail("Timed out waiting for standard projections to be written");

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
			_projectionsSubsystem = new ProjectionsSubsystem(1, runProjections: ProjectionType.All,
				startStandardProjections: false,
				projectionQueryExpiry: TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault),
				faultOutOfOrderProjections: Opts.FaultOutOfOrderProjectionsDefault);
			return new MiniNode(
				PathName, inMemDb: true, skipInitializeStandardUsersCheck: false,
				subsystems: new ISubsystem[] {_projectionsSubsystem});
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
		
		private List<string> _systemProjections =>
			typeof(ProjectionNamesBuilder.StandardProjections).GetFields(
					System.Reflection.BindingFlags.Public |
					System.Reflection.BindingFlags.Static |
					System.Reflection.BindingFlags.FlattenHierarchy)
				.Where(x => x.IsLiteral && !x.IsInitOnly)
				.Select(x => x.GetRawConstantValue().ToString())
				.ToList();
	}
}
