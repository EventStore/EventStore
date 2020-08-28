using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
using EventStore.Core.TransactionLog.Services;
using EventStore.Core.Util;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.ClientAPI.projectionsManager {
	public abstract class SpecificationWithNodeAndProjectionsManager : SpecificationWithDirectoryPerTestFixture {
		protected MiniNode _node;
		protected ProjectionsManager _projManager;
		protected IEventStoreConnection _connection;
		protected UserCredentials _credentials;
		protected TimeSpan _timeout;
		protected string _tag;
		private Task _systemProjectionsCreated;
		private ProjectionsSubsystem _projectionsSubsystem;


		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_credentials = new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword);
			_timeout = TimeSpan.FromSeconds(20);
			// Check if a node is running in ProjectionsManagerTestSuiteMarkerBase
			_tag = "_1";

			_node = CreateNode();
			await _node.Start().WithTimeout(_timeout);

			await _systemProjectionsCreated.WithTimeout(_timeout);

			_connection = TestConnection.Create(_node.TcpEndPoint);
			await _connection.ConnectAsync();

			_projManager = new ProjectionsManager(new ConsoleLogger(), _node.HttpEndPoint, _timeout, _node.HttpMessageHandler);
			try {
				await Given().WithTimeout(_timeout);
			} catch (Exception ex) {
				throw new Exception("Given Failed", ex);
			}

			try {
				await When().WithTimeout(_timeout);
			} catch (Exception ex) {
				throw new Exception("When Failed", ex);
			}
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			_connection.Close();
			await _node.Shutdown();

			await base.TestFixtureTearDown();
		}

		public abstract Task Given();
		public abstract Task When();

		protected MiniNode CreateNode() {
			_projectionsSubsystem = new ProjectionsSubsystem(1, runProjections: ProjectionType.All,
				startStandardProjections: false,
				projectionQueryExpiry: TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault),
				faultOutOfOrderProjections: Opts.FaultOutOfOrderProjectionsDefault);
			_systemProjectionsCreated = SystemProjections.Created(_projectionsSubsystem.LeaderMainBus);
			return new MiniNode(
				PathName, inMemDb: true, skipInitializeStandardUsersCheck: false,
				subsystems: new ISubsystem[] {_projectionsSubsystem});
		}

		protected EventData CreateEvent(string eventType, string data) {
			return new EventData(Guid.NewGuid(), eventType, true, Encoding.UTF8.GetBytes(data), null);
		}

		protected Task PostEvent(string stream, string eventType, string data) {
			return _connection.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { CreateEvent(eventType, data) });
		}

		protected Task CreateOneTimeProjection() {
			var query = CreateStandardQuery(Guid.NewGuid().ToString());
			return _projManager.CreateOneTimeAsync(query, _credentials);
		}

		protected Task CreateContinuousProjection(string projectionName) {
			var query = CreateStandardQuery(Guid.NewGuid().ToString());
			return _projManager.CreateContinuousAsync(projectionName, query, _credentials);
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
