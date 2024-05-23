#nullable enable

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;
using Serilog;

namespace EventStore.Core.Authorization;

public class PolicyAuthorizationProvider(IPolicyEvaluator policyEvaluator, bool logAuthorization = true, bool logSuccesses = false) : AuthorizationProviderBase {
	static readonly ILogger Logger = Log.ForContext<PolicyEvaluator>();
	
	static readonly TimeProvider Time = TimeProvider.System;

	public override async ValueTask<bool> CheckAccessAsync(ClaimsPrincipal principal, Operation operation, CancellationToken ct) {
		try {
			var startedAt = Time.GetTimestamp();
			
			var result = await policyEvaluator.EvaluateAsync(principal, operation, ct);

			var elapsedTime = Time.GetElapsedTime(startedAt);

			var accessGranted = result.Grant == Grant.Allow;

			if (!logAuthorization)
				return accessGranted;
			
			var identity = principal.FindFirstValue(ClaimTypes.Name) ?? "(anonymous)";

			if (!accessGranted)
				Logger.Warning(
					"Failed authorization check for {Identity} in {Duration} with {EvaluationResult}",
					identity, elapsedTime, result
				);
			else if (logSuccesses)
				Logger.Information(
					"Successful authorization check for {Identity} in {Duration} with {EvaluationResult}",
					identity, elapsedTime, result
				);

			return accessGranted;
		}
		catch (Exception ex) when (ex is not OperationCanceledException) {
			Logger.Error(
				ex, "Error performing permission check for {Identity}",
				principal.FindFirstValue(ClaimTypes.Name) ?? "unknown" // TODO SS: why "unknown" and not "(anonymous)"?!?
			);
			
			return false;
		}
	}
}