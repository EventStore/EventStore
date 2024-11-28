using System.Security.Claims;
using EventStore.Core.TransactionLog;

namespace EventStore.Core.Messages;

public static class ITransactionFileTrackerFactoryExtensions {
	public static ITransactionFileTracker For(
		this ITransactionFileTrackerFactory factory,
		string username) =>
		
		factory.GetOrAdd(username ?? "anonymous");

	public static ITransactionFileTracker For(
		this ITransactionFileTrackerFactory factory,
		ClaimsPrincipal user) =>
		
		factory.For(user?.Identity?.Name);

	public static ITransactionFileTracker For(
		this ITransactionFileTrackerFactory factory,
		ClientMessage.ReadRequestMessage msg) =>

		factory.For(msg?.User);
}
