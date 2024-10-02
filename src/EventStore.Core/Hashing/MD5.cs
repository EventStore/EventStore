// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Cryptography;
using EventStore.Common.Utils;
using EventStore.Plugins.MD5;

namespace EventStore.Core.Hashing;

public class MD5 {
	private static IMD5Provider _provider = new NetMD5Provider();

	public static HashAlgorithm Create() => _provider.Create();

	public static void UseProvider(IMD5Provider md5Provider) {
		Ensure.NotNull(md5Provider, nameof(md5Provider));

		try {
			using var _ = md5Provider.Create();
		} catch (Exception ex) {
			throw new AggregateException("Failed to use the specified MD5 provider.", ex);
		}

		_provider = md5Provider;
	}
}
