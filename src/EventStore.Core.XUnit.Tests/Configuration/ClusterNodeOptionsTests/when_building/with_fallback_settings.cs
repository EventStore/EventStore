// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Certificates;
using EventStore.Core.Configuration.Sources;
using EventStore.Core.Services;
using EventStore.Core.Tests;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EventStore.Core.XUnit.Tests.Configuration.ClusterNodeOptionsTests.when_building;

[TestFixture(typeof(LogFormat.V2), typeof(string))]
public class with_cluster_node_and_fallback_settings<TLogFormat, TStreamId> : SingleNodeScenario<TLogFormat,
		TStreamId> {
	protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) =>
		options with {
		};

	private IConfigurationRoot _configurationRoot;

	[Test]
	public void should_return_null_when_default_password_options_pass_through_environment_variables() {
		var prefix = KurrentConfigurationKeys.FallbackPrefix.ToUpper();
		var args = Array.Empty<string>();
		IDictionary environmentVariables = new Dictionary<string, string>();
		environmentVariables.Add($"{prefix}_DEFAULT_ADMIN_PASSWORD", "Admin#");
		environmentVariables.Add($"{prefix}_DEFAULT_OPS_PASSWORD", "Ops#");

		_configurationRoot = new ConfigurationBuilder()
			.AddKurrentDefaultValues(new Dictionary<string, object> {
				[nameof(ClusterVNodeOptions.DefaultUser.DefaultAdminPassword)] = SystemUsers.DefaultAdminPassword,
				[nameof(ClusterVNodeOptions.DefaultUser.DefaultOpsPassword)] = SystemUsers.DefaultOpsPassword
			})
			.AddKurrentCommandLine(args)
			.AddFallbackEnvironmentVariables(environmentVariables)
			.Build();

		var clusterVNodeOptions = ClusterVNodeOptions.FromConfiguration(_configurationRoot);

		Assert.Null(clusterVNodeOptions.CheckForEnvironmentOnlyOptions());
	}
}

[TestFixture(typeof(LogFormat.V2), typeof(string))]
public class
	with_single_node_and_fallback_settings<TLogFormat, TStreamId> : SingleNodeScenario<TLogFormat,
	TStreamId> {
	protected override ClusterVNodeOptions WithOptions(ClusterVNodeOptions options) => options with { };

	[Test]
	public void should_return_null_when_default_password_options_pass_through_environment_variables() {
		var prefix = KurrentConfigurationKeys.FallbackPrefix.ToUpper();
		var configuration = new ConfigurationBuilder()
			.AddKurrentDefaultValues()
			.AddKurrentEnvironmentVariables(
				($"{prefix}_DEFAULT_ADMIN_PASSWORD", "Admin#"),
				($"{prefix}_DEFAULT_OPS_PASSWORD", "Ops#")
			)
			.AddKurrentCommandLine()
			.Build();

		var options = ClusterVNodeOptions.FromConfiguration(configuration);

		var result = options.CheckForEnvironmentOnlyOptions();

		result.Should().BeNull();
	}

	[Test]
	public void ignores_subsection_arguments() {
		var prefix = KurrentConfigurationKeys.FallbackPrefix;
		var configuration = new ConfigurationBuilder()
			// we should be able to stop doing this soon as long as we bind the options automatically
			.AddKurrentDefaultValues()
			.AddKurrentEnvironmentVariables(
				($"{prefix.ToUpper()}__METRICS__X", "xxx"),
				($"{prefix.ToUpper()}__PLUGINS__Y", "yyy")
			)
			.AddKurrentCommandLine(
				$"--{prefix}:Metrics:A aaa " +
				$"--{prefix}:Plugins:B bbb"
			)
			.Build();

		var options = ClusterVNodeOptions.FromConfiguration(configuration);

		options.Unknown.Options.Should().BeEmpty();
	}
}
