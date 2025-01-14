// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using EventStore.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.TcpPlugin.Tests;

public class EventStoreOptionsTests {
	static EventStoreOptionsTests() {
		TypeDescriptor.AddAttributes(typeof(IPAddress), new TypeConverterAttribute(typeof(IPAddressConverter)));
	}

	private EventStoreOptions CreateSut(params (string Key, string? Value)[] options) {
		var sut = new ConfigurationBuilder()
			.AddInMemoryCollection(options.Select(
				pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
			.Build()
			.GetSection("EventStore")
			.Get<EventStoreOptions>() ?? new();

		return sut;
	}

	[Fact]
	public void when_empty_configuration() {
		var sut = CreateSut();
		Assert.False(sut.Insecure);
		Assert.False(sut.TcpPlugin.EnableExternalTcp);
	}

	[Fact]
	public void can_be_enabled() {
		var sut = CreateSut(
			("EventStore:TcpPlugin:EnableExternalTcp", "true"));
		Assert.True(sut.TcpPlugin.EnableExternalTcp);
	}

	[Fact]
	public void can_be_insecure() {
		var sut = CreateSut(
			("EventStore:Insecure", "true"));
		Assert.True(sut.Insecure);
	}

	[Fact]
	public void can_set_ip() {
		var sut = CreateSut(
			("EventStore:NodeIp", "1.2.3.4"));
		Assert.Equal(IPAddress.Parse("1.2.3.4"), sut.NodeIp);
	}
}
