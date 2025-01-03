// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests;

public class SpecificationWithFile {
	protected string Filename;

	[SetUp]
	public virtual Task SetUp() {
		var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
		Filename = Path.Combine(Path.GetTempPath(), string.Format("ES-{0}-{1}", Guid.NewGuid(), typeName));
		return Task.CompletedTask;
	}

	[TearDown]
	public virtual Task TearDown() {
		var task = Task.CompletedTask;
		try {
			if (File.Exists(Filename))
				File.Delete(Filename);
		} catch (Exception e) {
			task = Task.FromException(e);
		}

		return task;
	}
}
