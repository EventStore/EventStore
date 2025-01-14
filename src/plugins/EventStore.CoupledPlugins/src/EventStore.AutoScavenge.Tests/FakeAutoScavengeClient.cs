// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.AutoScavenge.Clients;
using EventStore.AutoScavenge.Domain;
using NCrontab;

namespace EventStore.AutoScavenge.Tests;

public class FakeAutoScavengeClient : IAutoScavengeClient {
	public ICommand? Command { get; private set; }

	public Task<Response<AutoScavengeStatusResponse>> GetStatus(CancellationToken token) {
		Command = new Commands.GetStatus(_ => {});
		return Task.FromResult(Response.Successful(new AutoScavengeStatusResponse(AutoScavengeStatusResponse.Status.NotConfigured, null, null)));
	}

	public Task<Response<Unit>> Pause(CancellationToken token) {
		Command = new Commands.PauseProcess(_ => { });
		return Task.FromResult(Response.Accepted());
	}

	public Task<Response<Unit>> Resume(CancellationToken token) {
		Command = new Commands.ResumeProcess(_ => { });
		return Task.FromResult(Response.Accepted());
	}

	public Task<Response<Unit>> Configure(CrontabSchedule schedule, CancellationToken token) {
		Command = new Commands.Configure(schedule, _ => { });
		return Task.FromResult(Response.Accepted());
	}
}
