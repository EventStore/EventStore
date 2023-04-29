using System;

namespace EventStore.Core.Util; 

public class RetriesExhaustedException : Exception {

	public RetriesExhaustedException(string message, Exception lastFailure) : base(message, lastFailure) {
	}
}
