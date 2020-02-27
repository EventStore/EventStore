using System.Security.Claims;
using System.Threading.Tasks;

namespace EventStore.Core.Authorization {
	public interface IAssertion {
		Grant Grant { get; }
		AssertionInformation Information { get; }

		ValueTask<bool> Evaluate(ClaimsPrincipal cp, Operation operation, PolicyInformation policy,
			EvaluationContext context);
	}
}
