﻿using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization {
	internal class OrAssertion : IAssertion {
		private readonly ReadOnlyMemory<IAssertion> _assertions;

		public OrAssertion(params IAssertion[] assertions) {
			_assertions = assertions.OrderBy(x => x.Grant).ToArray();
			Information = new AssertionInformation("or", $"({string.Join(",", _assertions)})", Grant.Unknown);
		}

		public AssertionInformation Information { get; }

		public Grant Grant { get; } = Grant.Unknown;

		public ValueTask<bool> Evaluate(ClaimsPrincipal cp, Operation operation, PolicyInformation policy,
			EvaluationContext context) {
			var remaining = _assertions;
			while (!remaining.IsEmpty && context.Grant != Grant.Deny) {
				var pending = remaining.Span[0].Evaluate(cp, operation, policy, context);
				remaining = remaining.Slice(1);
				if (!pending.IsCompleted)
					return EvaluateAsync(pending, remaining, cp, operation, policy, context);
				if (pending.Result) return new ValueTask<bool>(true);
			}

			return new ValueTask<bool>(false);
		}

		private async ValueTask<bool> EvaluateAsync(ValueTask<bool> pending, ReadOnlyMemory<IAssertion> remaining,
			ClaimsPrincipal cp, Operation operation, PolicyInformation policy, EvaluationContext result) {
			bool evaluated;
			while (!(evaluated = await pending.ConfigureAwait(false)) && !remaining.IsEmpty) {
				pending = remaining.Span[0].Evaluate(cp, operation, policy, result);
				remaining = remaining.Slice(1);
			}

			return evaluated;
		}
	}
}
