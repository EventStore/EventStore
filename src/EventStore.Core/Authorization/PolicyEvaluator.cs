using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization {
	public class MultiPolicyEvaluator : IPolicyEvaluator {
		private static readonly AssertionInformation DeniedByDefault =
			new AssertionInformation("default", "denied by default", Grant.Deny);

		private readonly ReadOnlyPolicy[] _policies;
		private readonly PolicyInformation _policyInfo;

		public MultiPolicyEvaluator(ReadOnlyPolicy[] policies) {
			_policies = policies;
			_policyInfo = new PolicyInformation("multi", 1, DateTimeOffset.MinValue);
		}

		public ValueTask<EvaluationResult>
			EvaluateAsync(ClaimsPrincipal cp, Operation operation, CancellationToken ct) {
			var evaluation = new EvaluationContext(operation, ct);

			foreach (var policy in _policies) {
				var policyInfo = policy.Information;
				if (policy.TryGetAssertions(operation, out var assertions))
					while (!assertions.IsEmpty && evaluation.Grant != Grant.Deny) {
						if (ct.IsCancellationRequested) break;
						var assertion = assertions.Span[0];
						assertions = assertions.Slice(1);
						var evaluate = assertion.Evaluate(cp, operation, policyInfo, evaluation);
						if (!evaluate.IsCompleted)
							return EvaluateAsync(evaluate, cp, operation, assertions, evaluation, ct);
					}
			}
			if (evaluation.Grant == Grant.Unknown) evaluation.Add(new AssertionMatch(_policyInfo, DeniedByDefault));

			return new ValueTask<EvaluationResult>(evaluation.ToResult());
		}

		private async ValueTask<EvaluationResult> EvaluateAsync(
			ValueTask<bool> pending,
			ClaimsPrincipal cp,
			Operation operation,
			ReadOnlyMemory<IAssertion> assertions,
			EvaluationContext evaluationContext,
			CancellationToken ct) {
			do {
				if (ct.IsCancellationRequested) break;
				await pending;
				if (ct.IsCancellationRequested) break;
				if (assertions.IsEmpty) break;
				pending = assertions.Span[0].Evaluate(cp, operation, _policyInfo, evaluationContext);
				assertions = assertions.Slice(1);
			} while (evaluationContext.Grant != Grant.Deny);

			if (evaluationContext.Grant == Grant.Unknown)
				evaluationContext.Add(new AssertionMatch(_policyInfo, DeniedByDefault));

			return evaluationContext.ToResult();
		}
	}
}
