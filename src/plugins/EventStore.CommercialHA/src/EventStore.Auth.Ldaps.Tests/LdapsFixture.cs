// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using Xunit.Abstractions;

namespace EventStore.Auth.Ldaps.Tests;

public class LdapsFixture : IDisposable{

	private const int InsecurePort = 10389;
	private const int SecurePort = 10636;

	private readonly ITestOutputHelper _output;
	private readonly IContainerService _ldapsServer;

	public LdapsFixture(ITestOutputHelper output) {
		_output = output;

		_ldapsServer = new Builder()
			.UseContainer()
			.UseImage("rroemhild/test-openldap")
			.WithName("es-openldap-server")
			.ExposePort(InsecurePort, InsecurePort)
			.ExposePort(SecurePort, SecurePort)
			.Build();
	}

	public void Start() {
		_ldapsServer.ShipContainerLogs(_output);
		_ldapsServer.Start();
	}

	public void Dispose() => _ldapsServer?.Dispose();
}
internal static class ContainerExtensions {
	public static void ShipContainerLogs(this IContainerService container, ITestOutputHelper testOutputHelper) =>
		Task.Run(() => {
			using var logs = container.Logs(true);
			foreach (var line in logs.ReadToEnd()) {
				testOutputHelper.WriteLine(line);
			}
		});
}
