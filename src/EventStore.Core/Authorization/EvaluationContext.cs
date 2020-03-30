﻿using System.Collections.Generic;
using System.Threading;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization {
	public class EvaluationContext {
		private readonly List<AssertionMatch> _matches;
		private readonly Operation _operation;

		public EvaluationContext(Operation operation, CancellationToken cancellationToken) {
			CancellationToken = cancellationToken;
			_operation = operation;
			_matches = new List<AssertionMatch>();
			Grant = Grant.Unknown;
		}

		public CancellationToken CancellationToken { get; }
		public Grant Grant { get; private set; }

		public void Add(AssertionMatch match) {
			if (match.Assertion.Grant > Grant)
				Grant = match.Assertion.Grant;
			_matches.Add(match);
		}

		public EvaluationResult ToResult() {
			return new EvaluationResult(_operation, Grant, _matches);
		}
	}
}
