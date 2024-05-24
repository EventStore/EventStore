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
	
	public override ValueTask<bool> CheckAccessAsync(ClaimsPrincipal principal, Operation operation, CancellationToken ct) {
		var startedAt = Time.GetTimestamp();
		
		var evaluateTask = policyEvaluator.EvaluateAsync(principal, operation, ct);

		if (evaluateTask.IsCompletedSuccessfully) {
			var result = evaluateTask.Result;
			var accessGranted = result.Grant == Grant.Allow;

			if (!logAuthorization)
				return new(accessGranted);

			if (accessGranted && logSuccesses)
				Logger.Information(
					"Successful authorization check for {Identity} in {Duration} with {EvaluationResult}",
					GetIdentity(principal), Time.GetElapsedTime(startedAt), result
				);
			else if (!accessGranted)
				Logger.Warning(
					"Failed authorization check for {Identity} in {Duration} with {EvaluationResult}",
					GetIdentity(principal), Time.GetElapsedTime(startedAt), result
				);

			return new(accessGranted);
		}

		return CaptureError(evaluateTask, GetIdentity(principal));

		static string GetIdentity(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.Name) ?? "(anonymous)";
		
		static async ValueTask<bool> CaptureError(ValueTask<EvaluationResult> evaluateTask, string identity) {
			try {
				await evaluateTask;
			}
			catch (Exception ex) when (ex is not OperationCanceledException) {
				Logger.Error(ex, "Error performing permission check for {Identity}", identity);
			}
			
			return false;
		}
	}
}