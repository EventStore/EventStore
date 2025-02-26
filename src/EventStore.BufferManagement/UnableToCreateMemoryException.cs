// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;

namespace EventStore.BufferManagement;

public class UnableToCreateMemoryException : Exception {
	public UnableToCreateMemoryException()
		: base("All buffers were in use and acquiring more memory has been disabled.") {
	}
}
