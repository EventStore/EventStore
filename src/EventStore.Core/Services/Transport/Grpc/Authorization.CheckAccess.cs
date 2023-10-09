using System.Linq;
using System.Threading.Tasks;
using EventStore.Client.Authorization;
using Grpc.Core;

using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

using Operation = EventStore.Plugins.Authorization.Operation;
using Parameter = EventStore.Plugins.Authorization.Parameter;

namespace EventStore.Core.Services.Transport.Grpc;

internal partial class Authorization {
	private static readonly Operation CheckAccessOperation = new(Plugins.Authorization.Operations.Authorization.CheckAccess);

	public override async Task<CheckAccessResp> CheckAccess(CheckAccessReq request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _provider.CheckAccessAsync(user, CheckAccessOperation, context.CancellationToken).ConfigureAwait(false)) {
			throw RpcExceptions.AccessDenied();
		}

		var cp = BuildClaimsPrincipal(request);
		var op = BuildOperation(request);
		var isAllowed = await _provider.CheckAccessAsync(cp, op, context.CancellationToken).ConfigureAwait(false);

		return new CheckAccessResp {
			IsAllowed = isAllowed
		};
	}

	private static ClaimsPrincipal BuildClaimsPrincipal(CheckAccessReq request) {
		return new ClaimsPrincipal(
			request.ClaimsPrincipal.Identities
				.Select(
					identity => identity.Claims.Select(
						claim => new Claim(
							type: claim.Type,
							value: claim.Value,
							valueType: claim.ValueType,
							issuer: claim.Issuer,
							originalIssuer: claim.OriginalIssuer)))
				.Select(claims => new ClaimsIdentity(claims)));
	}

	private static Operation BuildOperation(CheckAccessReq request) {
		var requestOp = request.Operation;
		var op = new Operation(requestOp.Resource, requestOp.Action);

		if (request.Operation.Parameters != null) {
			var parameters = requestOp.Parameters
					.Select(p => new Parameter(p.Name, p.Value))
					.ToArray();

			op = op.WithParameters(parameters);
		}

		return op;
	}
}
