using System.Collections.Generic;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Index {
	public class FakeFilenameProvider : IIndexFilenameProvider {
		private List<string> _filenames = new List<string>();
		private int _current;


		public FakeFilenameProvider(params string[] fakenames) {
			_filenames.AddRange(fakenames);
		}

		public string GetFilenameNewTable() {
			var ret = _filenames[_current];
			_current++;
			return ret;
		}
	}
}
