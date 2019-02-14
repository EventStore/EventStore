using System;

namespace EventStore.Core.Exceptions {
	class SystemStreamNotFoundException : Exception {
		public SystemStreamNotFoundException(string message) : base(message) {
		}
	}
}
