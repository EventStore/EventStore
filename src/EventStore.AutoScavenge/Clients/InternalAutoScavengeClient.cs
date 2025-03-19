// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Channels;
using EventStore.AutoScavenge.Domain;
using NCrontab;

namespace EventStore.AutoScavenge.Clients;

// Manages the local auto scavenge by putting commands on the channel
public class InternalAutoScavengeClient(ChannelWriter<ICommand> channel) : IAutoScavengeClient {
	public Task<Response<AutoScavengeStatusResponse>> GetStatus(CancellationToken token) =>
		Execute<AutoScavengeStatusResponse>(r => new Commands.GetStatus(r), token);

	public Task<Response<Unit>> Pause(CancellationToken token) =>
		Execute<Unit>(r => new Commands.PauseProcess(r), token);

	public Task<Response<Unit>> Resume(CancellationToken token) =>
		Execute<Unit>(r => new Commands.ResumeProcess(r), token);

	public Task<Response<Unit>> Configure(CrontabSchedule schedule, CancellationToken token) =>
		Execute<Unit>(r => new Commands.Configure(schedule, r), token);

	private async Task<Response<TResp>> Execute<TResp>(Func<Action<Response<TResp>>, ICommand> make, CancellationToken token) {
		var source = new TaskCompletionSource<Response<TResp>>();

		await using var _ = token.Register(() => source.TrySetCanceled(token));
		var command = make(source.SetResult);
		if (!channel.TryWrite(command))
			return Response.ServerError<TResp>("AutoScavenge queue is full");

		return await source.Task;
	}
}
