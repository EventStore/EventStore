// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using System.Runtime;
using EventStore.Common.Utils;

namespace EventStore.Core.Util;

public static class DefaultFiles {
    public static readonly string DefaultConfigFile = RuntimeInformation.IsWindows
        ? string.Empty
        : "kurrentdb.conf";

    public static readonly string LegacyEventStoreConfigFile = RuntimeInformation.IsWindows
	    ? string.Empty
	    : "eventstore.conf";

    public static string DefaultConfigPath =>
	    Path.Combine(Locations.DefaultConfigurationDirectory, DefaultConfigFile);
    public static string LegacyEventStoreConfigPath =>
	    Path.Combine(Locations.LegacyConfigurationDirectory, LegacyEventStoreConfigFile);
}
