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
