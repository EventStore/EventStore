using System;

namespace EventStore.Core.Exceptions {
	public class PTableNotFoundException : Exception {
		public PTableNotFoundException(string ptableName) : base("PTable " + ptableName + " not found.") {
		}
	}
}
