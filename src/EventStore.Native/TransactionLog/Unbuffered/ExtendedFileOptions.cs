// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.TransactionLog.Unbuffered;

[Flags]
public enum ExtendedFileOptions {
	NoBuffering = unchecked((int)0x20000000),
	Overlapped = unchecked((int)0x40000000),
	SequentialScan = unchecked((int)0x08000000),
	WriteThrough = unchecked((int)0x80000000)
}
