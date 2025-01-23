// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Tests.Fakes;

namespace EventStore.Core.Tests.Services.Transport.Http;

public class HttpBootstrap {
	public static void RegisterPing(IUriRouter service) {
		service.RegisterController(new PingController());
	}

	public static void RegisterStat(IUriRouter service) {
		service.RegisterController(new StatController(new FakePublisher(), new FakePublisher()));
	}
}
