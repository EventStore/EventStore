using System.Linq;
using System.Threading.Tasks;
using EventStore.Client.Authorization;
using Grpc.Core;

using Claim = EventStore.Client.Authorization.Claim;
using ClaimsIdentity = EventStore.Client.Authorization.ClaimsIdentity;
using ClaimsPrincipal = EventStore.Client.Authorization.ClaimsPrincipal;

using Operation = EventStore.Plugins.Authorization.Operation;

namespace EventStore.Core.Services.Transport.Grpc; 

internal partial class Authorization {
	private static readonly Operation GetClaimsOperation = new(Plugins.Authorization.Operations.Authorization.GetClaims);

	public override async Task<ClaimsResp> GetClaims(ClaimsReq request, ServerCallContext context) {
		var user = context.GetHttpContext().User;
		if (!await _provider.CheckAccessAsync(user, GetClaimsOperation, context.CancellationToken).ConfigureAwait(false)) {
			throw RpcExceptions.AccessDenied();
		}

		var claimsPrincipal = new ClaimsPrincipal {
			Identities = {
				user.Identities
					.Select(
						identity => identity.Claims.Select(
							claim => new Claim {
								Type = claim.Type,
								Value = claim.Value,
								ValueType = claim.ValueType,
								Issuer = claim.Issuer,
								OriginalIssuer = claim.OriginalIssuer
							}))
					.Select(claims => new ClaimsIdentity {
						Claims = { claims }
					})
			}
		};

		return new ClaimsResp {
			ClaimsPrincipal = claimsPrincipal
		};
	}
}
