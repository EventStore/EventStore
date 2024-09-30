// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Cryptography;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using EventStore.Core.Index;
using EventStore.Common.Utils;
using EventStore.Common.Options;

namespace EventStore.Core.Tests.Index.IndexV4 {
	[TestFixture(PTable.IndexEntryV4Size), Explicit]
	public class
		opening_a_ptable_with_more_than_32bits_of_records : IndexV1.opening_a_ptable_with_more_than_32bits_of_records {
		public opening_a_ptable_with_more_than_32bits_of_records(int indexEntrySize) : base(indexEntrySize) {
		}
	}
}
