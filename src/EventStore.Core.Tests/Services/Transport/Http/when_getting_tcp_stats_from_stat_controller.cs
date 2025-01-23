// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Transport.Http.Codecs;
using NUnit.Framework;
using EventStore.Core.Tests.ClientAPI;
using HttpMethod = System.Net.Http.HttpMethod;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Services.Transport.Http;

[TestFixture(typeof(LogFormat.V2), typeof(string))]
[TestFixture(typeof(LogFormat.V3), typeof(uint))]
public class when_getting_tcp_stats_from_stat_controller<TLogFormat, TStreamId>
	: SpecificationWithMiniNode<TLogFormat, TStreamId> {
	private IEventStoreConnection _connection;
	private string _url;
	const string ClientConnectionName = "test-connection";

	private List<MonitoringMessage.TcpConnectionStats> _results = [];
	private HttpResponseMessage _response;

	protected override async Task Given() {
		_url = _node.HttpEndPoint.ToHttpUrl(Uri.UriSchemeHttp, "/stats/tcp");

		var settings = ConnectionSettings.Create().DisableServerCertificateValidation();
		_connection = EventStoreConnection.Create(settings, _node.TcpEndPoint, ClientConnectionName);
		await _connection.ConnectAsync();

		var testEvent = new EventData(Guid.NewGuid(), "TestEvent", true,
			Encoding.ASCII.GetBytes("{'Test' : 'OneTwoThree'}"), null);
		await _connection.AppendToStreamAsync("tests", ExpectedVersion.Any, testEvent);
	}

	protected override async Task When() {
		_response = await _node.HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, _url));
		_results = Codec.Json.From<List<MonitoringMessage.TcpConnectionStats>>(
			await _response.Content.ReadAsStringAsync());
	}

	[Test]
	public void should_have_succeeded() {
		Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
	}

	[Test]
	public void should_return_the_external_connections() {
		Assert.AreEqual(2, _results.Count(r => r.IsExternalConnection));
	}

	[Test]
	public void should_return_the_total_number_of_bytes_sent_for_external_connections() {
		Assert.Greater(_results.Sum(r => r.IsExternalConnection ? r.TotalBytesSent : 0), 0);
	}

	[Test]
	public void should_return_the_total_number_of_bytes_received_from_external_connections() {
		Assert.Greater(_results.Sum(r => r.IsExternalConnection ? r.TotalBytesReceived : 0), 0);
	}

	[Test]
	public void should_have_set_the_client_connection_name() {
		Assert.IsTrue(_results.Any(x => x.ClientConnectionName == ClientConnectionName));
	}

	[OneTimeTearDown]
	public override Task TestFixtureTearDown() {
		_connection.Dispose();
		return base.TestFixtureTearDown();
	}
}
