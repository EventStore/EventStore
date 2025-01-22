// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Services.Archive.Storage.Exceptions;

public class ChunkDeletedException : Exception;

//qq consider location. do we want to couple this to etags specifically? maybe rename to BrokenHandleException?
public class WrongETagException(string objectName, string expected, string actual) : Exception(
	$"Wrong ETag for \"{objectName}\". Expected \"{expected}\" but was \"{actual}\"");
