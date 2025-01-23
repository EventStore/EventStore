// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using System.Threading.Tasks;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace EventStore.Core.Tests.Services.Transport.Http;

[TestFixture, Category("LongRunning")]
public class http_service_should : SpecificationWithDirectory {
	[Test]
	[Category("Network")]
	public async Task handle_invalid_characters_in_url() {
		await using var node = new MiniNode<LogFormat.V2,string>(PathName);
		node.Node.MainQueue.Publish(new SystemMessage.SystemInit());

		var result = await node.HttpClient.GetAsync("/ping^\"");

		Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
		Assert.IsEmpty(await result.Content.ReadAsStringAsync());
	}
}

[TestFixture, Category("LongRunning")]
public class when_http_request_times_out : SpecificationWithDirectory {
	[Test]
	[Category("Network")]
	public async Task should_throw_an_exception() {
		const int timeoutSec = 2;
		var sleepFor = timeoutSec + 1;

		await using var node = new MiniNode<LogFormat.V2, string>(PathName, httpClientTimeoutSec: timeoutSec);
		await node.Start();

		Assert.ThrowsAsync<TaskCanceledException>(() => node.HttpClient.GetAsync($"/test-timeout?sleepfor={sleepFor * 1000}"),
			message: "The client aborted the request.");
	}
}
