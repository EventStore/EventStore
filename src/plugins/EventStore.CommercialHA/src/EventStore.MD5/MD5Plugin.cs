// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.ComponentModel.Composition;
using EventStore.Plugins.MD5;

namespace EventStore.MD5;

[Export(typeof(IMD5Plugin))]
public class MD5Plugin : IMD5Plugin {
	public string Name => "MD5";
	public string Version => typeof(MD5Plugin).Assembly.GetName().Version!.ToString();
	public string CommandLineName => "md5";
	public IMD5ProviderFactory GetMD5ProviderFactory() => new MD5ProviderFactory();
}
