// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization;

public class MultiPolicyEvaluator : IPolicyEvaluator {
	private static readonly AssertionInformation DeniedByDefault = new("default", "denied by default", Grant.Deny);

	private readonly IPolicySelector[] _policySelectors;
	private readonly PolicyInformation _policyInfo;

	public MultiPolicyEvaluator(IPolicySelector[] policySelectors) {
		_policySelectors = policySelectors;
		_policyInfo = new PolicyInformation("multi-policy", 1, DateTimeOffset.MinValue);
	}

	public async ValueTask<EvaluationResult>
		EvaluateAsync(ClaimsPrincipal cp, Operation operation, CancellationToken ct) {
		var evaluationContext = new EvaluationContext(operation, ct);
		foreach (var policySelector in _policySelectors) {
			var policy = policySelector.Select();
			var policyInfo = policy.Information;
			if (policy.TryGetAssertions(operation, out var assertions)) {
				while (!assertions.IsEmpty && evaluationContext.Grant != Grant.Deny) {
					if (ct.IsCancellationRequested) break;
					var assertion = assertions.Span[0];
					assertions = assertions.Slice(1);
					await assertion.Evaluate(cp, operation, policyInfo, evaluationContext);
				}
			}

			if (evaluationContext.Grant != Grant.Unknown) {
				return evaluationContext.ToResult();
			}
		}

		evaluationContext.Add(new AssertionMatch(_policyInfo, DeniedByDefault));
		return evaluationContext.ToResult();
	}
}
