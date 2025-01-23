// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace EventStore.Core.Tests;

[TestFixture]
public class VerifyIntPtrSize {
	[Test]
	public void TestIntPtrSize() {
		Assert.AreEqual(8, IntPtr.Size);
	}
}

public static class WebHostBuilderExtensions {
	public static IWebHostBuilder UseStartup(this IWebHostBuilder builder, IInternalStartup startup)
		=> builder.ConfigureServices(services => services.AddSingleton(startup));
}
