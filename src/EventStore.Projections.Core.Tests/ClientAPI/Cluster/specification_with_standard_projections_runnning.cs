﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.SystemData;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Tests;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Util;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.ClientAPI.ResolvedEvent;
using EventStore.ClientAPI.Projections;
using System.Threading.Tasks;
using EventStore.Core.Data;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;

namespace EventStore.Projections.Core.Tests.ClientAPI.Cluster {
	[Category("ClientAPI")]
	public class specification_with_standard_projections_runnning : SpecificationWithDirectoryPerTestFixture {
		protected MiniClusterNode[] _nodes = new MiniClusterNode[3];
		protected Endpoints[] _nodeEndpoints = new Endpoints[3];
		protected IEventStoreConnection _conn;
		private readonly ProjectionsSubsystem[] _projections = new ProjectionsSubsystem[3];
		protected UserCredentials _admin = DefaultData.AdminCredentials;
		protected ProjectionsManager _manager;

		protected class Endpoints {
			public readonly IPEndPoint InternalTcp;
			public readonly IPEndPoint InternalTcpSec;
			public readonly IPEndPoint InternalHttp;
			public readonly IPEndPoint ExternalTcp;
			public readonly IPEndPoint ExternalTcpSec;
			public readonly IPEndPoint ExternalHttp;
			private readonly int[] _ports;

			public Endpoints(
				int internalTcp, int internalTcpSec, int internalHttp, int externalTcp,
				int externalTcpSec, int externalHttp) {
				var testIp = Environment.GetEnvironmentVariable("ES-TESTIP");

				var address = string.IsNullOrEmpty(testIp) ? IPAddress.Loopback : IPAddress.Parse(testIp);
				InternalTcp = new IPEndPoint(address, internalTcp);
				InternalTcpSec = new IPEndPoint(address, internalTcpSec);
				InternalHttp = new IPEndPoint(address, internalHttp);
				ExternalTcp = new IPEndPoint(address, externalTcp);
				ExternalTcpSec = new IPEndPoint(address, externalTcpSec);
				ExternalHttp = new IPEndPoint(address, externalHttp);

				_ports = new[]
					{internalHttp, internalTcp, internalTcpSec, externalHttp, externalTcp, externalTcpSec};
			}

			public IEnumerable<int> Ports => _ports;
		}

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
#if (!DEBUG)
            Assert.Ignore("These tests require DEBUG conditional");
#else
			_nodeEndpoints[0] = new Endpoints(
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));
			_nodeEndpoints[1] = new Endpoints(
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));
			_nodeEndpoints[2] = new Endpoints(
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback),
				PortsHelper.GetAvailablePort(IPAddress.Loopback), PortsHelper.GetAvailablePort(IPAddress.Loopback));

			_nodes[0] = CreateNode(0,
				_nodeEndpoints[0], new[] { _nodeEndpoints[1].InternalHttp, _nodeEndpoints[2].InternalHttp });
			_nodes[1] = CreateNode(1,
				_nodeEndpoints[1], new[] { _nodeEndpoints[0].InternalHttp, _nodeEndpoints[2].InternalHttp });
			_nodes[2] = CreateNode(2,
				_nodeEndpoints[2], new[] { _nodeEndpoints[0].InternalHttp, _nodeEndpoints[1].InternalHttp });
			WaitIdle();

			var projectionsStarted = _projections.Select(p => SystemProjections.Created(p.MasterMainBus)).ToArray();

			foreach (var node in _nodes) {
				node.Start();
				node.WaitIdle();
			}

			await Task.WhenAll(_nodes.Select(x => x.Started)).WithTimeout(TimeSpan.FromSeconds(30));

			_conn = EventStoreConnection.Create(_nodes[0].ExternalTcpEndPoint);
			await _conn.ConnectAsync().WithTimeout();

			_manager = new ProjectionsManager(
				new ConsoleLogger(),
				_nodes.Single(x => x.NodeState == VNodeState.Master).ExternalHttpEndPoint,
				TimeSpan.FromMilliseconds(10000));

			if (GivenStandardProjectionsRunning()) {
				await Task.WhenAny(projectionsStarted).WithTimeout(TimeSpan.FromSeconds(10));
				await EnableStandardProjections().WithTimeout(TimeSpan.FromMinutes(2));
			}

			WaitIdle();

			try {
				await Given().WithTimeout();
			} catch (Exception ex) {
				throw new Exception("Given Failed", ex);
			}

			try {
				await When().WithTimeout();
			} catch (Exception ex) {
				throw new Exception("When Failed", ex);
			}
#endif
		}

		private MiniClusterNode CreateNode(int index, Endpoints endpoints, IPEndPoint[] gossipSeeds) {
			_projections[index] = new ProjectionsSubsystem(1, runProjections: ProjectionType.All,
				startStandardProjections: false,
				projectionQueryExpiry: TimeSpan.FromMinutes(Opts.ProjectionsQueryExpiryDefault),
				faultOutOfOrderProjections: Opts.FaultOutOfOrderProjectionsDefault);
			var node = new MiniClusterNode(
				PathName, index, endpoints.InternalTcp, endpoints.InternalTcpSec, endpoints.InternalHttp,
				endpoints.ExternalTcp,
				endpoints.ExternalTcpSec, endpoints.ExternalHttp, skipInitializeStandardUsersCheck: false,
				subsystems: new ISubsystem[] { _projections[index] }, gossipSeeds: gossipSeeds);
			return node;
		}

		[TearDown]
		public async Task PostTestAsserts() {
			var all = await _manager.ListAllAsync(_admin);
			if (all.Any(p => p.Name == "Faulted"))
				Assert.Fail("Projections faulted while running the test" + "\r\n" + all);
		}

		protected async Task EnableStandardProjections() {
			await EnableProjection(ProjectionNamesBuilder.StandardProjections.EventByCategoryStandardProjection);
			await EnableProjection(ProjectionNamesBuilder.StandardProjections.EventByTypeStandardProjection);
			await EnableProjection(ProjectionNamesBuilder.StandardProjections.StreamByCategoryStandardProjection);
			await EnableProjection(ProjectionNamesBuilder.StandardProjections.StreamsStandardProjection);
		}

		protected async Task DisableStandardProjections() {
			await DisableProjection(ProjectionNamesBuilder.StandardProjections.EventByCategoryStandardProjection);
			await DisableProjection(ProjectionNamesBuilder.StandardProjections.EventByTypeStandardProjection);
			await DisableProjection(ProjectionNamesBuilder.StandardProjections.StreamByCategoryStandardProjection);
			await DisableProjection(ProjectionNamesBuilder.StandardProjections.StreamsStandardProjection);
		}

		protected virtual bool GivenStandardProjectionsRunning() {
			return true;
		}

		protected async Task EnableProjection(string name) {
			for (int i = 1; i <= 10; i++) {
				try {
					await _manager.EnableAsync(name, _admin);
				} catch (Exception) {
					if (i == 10)
						throw;
					await Task.Delay(5000);
				}
			}

			await Task.Delay(1000); /* workaround for race condition when multiple projections are being enabled simultaneously */
		}

		protected Task DisableProjection(string name) {
			return _manager.DisableAsync(name, _admin);
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			_conn.Close();
			await Task.WhenAll(
				_nodes[0].Shutdown(),
				_nodes[1].Shutdown(),
				_nodes[2].Shutdown());
			foreach (var endpoint in _nodeEndpoints) {
				foreach (var port in endpoint.Ports) {
					PortsHelper.ReturnPort(port);
				}
			}
			await base.TestFixtureTearDown();
		}

		protected virtual Task When() => Task.CompletedTask;

		protected virtual Task Given() => Task.CompletedTask;

		protected Task PostEvent(string stream, string eventType, string data) {
			return _conn.AppendToStreamAsync(stream, ExpectedVersion.Any, new[] { CreateEvent(eventType, data) });
		}

		protected Task HardDeleteStream(string stream) {
			return _conn.DeleteStreamAsync(stream, ExpectedVersion.Any, true, _admin);
		}

		protected Task SoftDeleteStream(string stream) {
			return _conn.DeleteStreamAsync(stream, ExpectedVersion.Any, false, _admin);
		}

		protected static EventData CreateEvent(string type, string data) {
			return new EventData(Guid.NewGuid(), type, true, Encoding.UTF8.GetBytes(data), new byte[0]);
		}

		protected void WaitIdle() {
#if DEBUG
			_nodes[0].WaitIdle();
			_nodes[1].WaitIdle();
			_nodes[2].WaitIdle();
#endif
		}

		protected async Task AssertStreamTailAsync(string streamId, params string[] events) {
#if DEBUG
			var result = await _conn.ReadStreamEventsBackwardAsync(streamId, -1, events.Length, true, _admin);
			switch (result.Status) {
				case SliceReadStatus.StreamDeleted:
					Assert.Fail("Stream '{0}' is deleted", streamId);
					break;
				case SliceReadStatus.StreamNotFound:
					Assert.Fail("Stream '{0}' does not exist", streamId);
					break;
				case SliceReadStatus.Success:
					var resultEventsReversed = result.Events.Reverse().ToArray();
					if (resultEventsReversed.Length < events.Length)
						DumpFailed("Stream does not contain enough events", streamId, events, result.Events);
					else {
						for (var index = 0; index < events.Length; index++) {
							var parts = events[index].Split(new char[] { ':' }, 2);
							var eventType = parts[0];
							var eventData = parts[1];

							if (resultEventsReversed[index].Event.EventType != eventType)
								DumpFailed("Invalid event type", streamId, events, resultEventsReversed);
							else if (resultEventsReversed[index].Event.DebugDataView != eventData)
								DumpFailed("Invalid event body", streamId, events, resultEventsReversed);
						}
					}

					break;
			}
#endif
		}

		protected async Task DumpStreamAsync(string streamId) {
#if DEBUG
			var result = await _conn.ReadStreamEventsBackwardAsync(streamId, -1, 100, true, _admin);
			switch (result.Status) {
				case SliceReadStatus.StreamDeleted:
					Assert.Fail("Stream '{0}' is deleted", streamId);
					break;
				case SliceReadStatus.StreamNotFound:
					Assert.Fail("Stream '{0}' does not exist", streamId);
					break;
				case SliceReadStatus.Success:
					Dump("Dumping..", streamId, result.Events.Reverse().ToArray());
					break;
			}
#endif
		}

#if DEBUG
		private void DumpFailed(string message, string streamId, string[] events, ResolvedEvent[] resultEvents) {
			var expected = events.Aggregate("", (a, v) => a + ", " + v);
			var actual = resultEvents.Aggregate(
				"", (a, v) => a + ", " + v.Event.EventType + ":" + v.Event.DebugDataView);

			var actualMeta = resultEvents.Aggregate(
				"", (a, v) => a + "\r\n" + v.Event.EventType + ":" + v.Event.DebugMetadataView);


			Assert.Fail(
				"Stream: '{0}'\r\n{1}\r\n\r\nExisting events: \r\n{2}\r\n Expected events: \r\n{3}\r\n\r\nActual metas:{4}",
				streamId,
				message, actual, expected, actualMeta);
		}

		private void Dump(string message, string streamId, ResolvedEvent[] resultEvents) {
			var actual = resultEvents.Aggregate(
				"", (a, v) => a + ", " + v.OriginalEvent.EventType + ":" + v.OriginalEvent.DebugDataView);

			var actualMeta = resultEvents.Aggregate(
				"", (a, v) => a + "\r\n" + v.OriginalEvent.EventType + ":" + v.OriginalEvent.DebugMetadataView);

			Debug.WriteLine(
				"Stream: '{0}'\r\n{1}\r\n\r\nExisting events: \r\n{2}\r\n \r\nActual metas:{3}", streamId,
				message, actual, actualMeta);
		}
#endif

		protected async Task PostProjection(string query) {
			await _manager.CreateContinuousAsync("test-projection", query, _admin);
			WaitIdle();
		}
	}

	[TestFixture, Explicit]
	public class TestTest : specification_with_standard_projections_runnning {
		[Test, Explicit]
		public async Task Test() {
			await PostProjection(@"fromStream('$user-admin').outputState()");
			await AssertStreamTailAsync("$projections-test-projection-result", "Result:{}");
		}
	}
}
