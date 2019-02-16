using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Index {
	public static class IndexMapTestFactory {
		public static IndexMap FromFile(string filename, int maxTablesPerLevel = 4,
			bool loadPTables = true, int cacheDepth = 16, bool skipIndexVerify = false,
			int threads = 1,
			int maxAutoMergeLevel = int.MaxValue) {
			return IndexMap.FromFile(filename, maxTablesPerLevel, loadPTables, cacheDepth, skipIndexVerify, threads,
				maxAutoMergeLevel);
		}
	}
}
