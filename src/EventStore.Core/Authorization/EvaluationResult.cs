﻿using System;
using System.Collections.Generic;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization {
	public readonly struct EvaluationResult {
		public readonly Grant Grant;
		public readonly Operation Operation;
		public readonly IReadOnlyList<AssertionMatch> Matches;

		public EvaluationResult(Operation operation, Grant grant, params AssertionMatch[] matches) : this(operation,
			grant, (IReadOnlyList<AssertionMatch>)matches) {
		}

		public EvaluationResult(Operation operation, Grant grant, IReadOnlyList<AssertionMatch> matches) {
			Grant = grant;
			Matches = matches;
			Operation = operation;
		}

		public override string ToString() {
			return $"{Operation} {Grant} : {string.Join(Environment.NewLine, Matches)}";
		}
	}
}
