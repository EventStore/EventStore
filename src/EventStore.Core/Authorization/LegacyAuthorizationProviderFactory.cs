﻿using System;
using System.Security.Claims;
using EventStore.Core.Bus;
using EventStore.Core.Services;
using EventStore.Plugins.Authorization;
using Serilog;

namespace EventStore.Core.Authorization {
	public class LegacyAuthorizationProviderFactory : IAuthorizationProviderFactory {
		private readonly IPublisher _mainQueue;

		private static readonly Claim[] Admins =
			{new Claim(ClaimTypes.Role, SystemRoles.Admins), new Claim(ClaimTypes.Name, SystemUsers.Admin)};

		private static readonly Claim[] OperationsOrAdmins = {
			new Claim(ClaimTypes.Role, SystemRoles.Admins), new Claim(ClaimTypes.Name, SystemUsers.Admin),
			new Claim(ClaimTypes.Role, SystemRoles.Operations), new Claim(ClaimTypes.Name, SystemUsers.Operations)
		};

		public LegacyAuthorizationProviderFactory(IPublisher mainQueue) {
			_mainQueue = mainQueue;
		}

		public IAuthorizationProvider Build() {
			var policy = new Policy("Legacy", 1, DateTimeOffset.MinValue);
			var streamAssertion = new LegacyStreamPermissionAssertion(_mainQueue);

			policy.AllowAnonymous(Operations.Node.Redirect);
			policy.AllowAnonymous(Operations.Node.StaticContent);
			policy.AllowAnonymous(Operations.Node.Ping);
			policy.AllowAnonymous(Operations.Node.Options);

			policy.AllowAnonymous(Operations.Node.Information.Read);
			policy.AddMatchAnyAssertion(Operations.Node.Information.Subsystems, Grant.Allow, OperationsOrAdmins);
			policy.AddMatchAnyAssertion(Operations.Node.Information.Histogram, Grant.Allow, OperationsOrAdmins);
			policy.AddMatchAnyAssertion(Operations.Node.Information.Options, Grant.Allow, OperationsOrAdmins);

			policy.AllowAnonymous(Operations.Node.Statistics.Read);
			policy.AllowAnonymous(Operations.Node.Statistics.Replication);
			policy.AllowAnonymous(Operations.Node.Statistics.Tcp);
			policy.AllowAnonymous(Operations.Node.Statistics.Custom);

			policy.AllowAnonymous(Operations.Node.Elections.Prepare);
			policy.AllowAnonymous(Operations.Node.Elections.PrepareOk);
			policy.AllowAnonymous(Operations.Node.Elections.ViewChange);
			policy.AllowAnonymous(Operations.Node.Elections.ViewChangeProof);
			policy.AllowAnonymous(Operations.Node.Elections.Proposal);
			policy.AllowAnonymous(Operations.Node.Elections.Accept);
			policy.AllowAnonymous(Operations.Node.Elections.LeaderIsResigning);
			policy.AllowAnonymous(Operations.Node.Elections.LeaderIsResigningOk);

			policy.AllowAnonymous(Operations.Node.Gossip.Read);
			policy.AllowAnonymous(Operations.Node.Gossip.Update);

			policy.AddMatchAnyAssertion(Operations.Node.Shutdown, Grant.Allow, OperationsOrAdmins);
			policy.AddMatchAnyAssertion(Operations.Node.MergeIndexes, Grant.Allow, OperationsOrAdmins);
			policy.AddMatchAnyAssertion(Operations.Node.SetPriority, Grant.Allow, OperationsOrAdmins);
			policy.AddMatchAnyAssertion(Operations.Node.Resign, Grant.Allow, OperationsOrAdmins);
			policy.AddMatchAnyAssertion(Operations.Node.Scavenge.Start, Grant.Allow, OperationsOrAdmins);
			policy.AddMatchAnyAssertion(Operations.Node.Scavenge.Stop, Grant.Allow, OperationsOrAdmins);

			var subscriptionAccess =
				new AndAssertion(
					new RequireAuthenticatedAssertion(),
					new RequireStreamReadAssertion(streamAssertion));

			policy.RequireAuthenticated(Operations.Subscriptions.Statistics);
			policy.AddMatchAnyAssertion(Operations.Subscriptions.Create, Grant.Allow, OperationsOrAdmins);
			policy.AddMatchAnyAssertion(Operations.Subscriptions.Update, Grant.Allow, OperationsOrAdmins);
			policy.AddMatchAnyAssertion(Operations.Subscriptions.Delete, Grant.Allow, OperationsOrAdmins);
			policy.Add(Operations.Subscriptions.ProcessMessages, subscriptionAccess);
			policy.AddMatchAnyAssertion(Operations.Subscriptions.ReplayParked, Grant.Allow, OperationsOrAdmins);

			policy.Add(Operations.Streams.Read, streamAssertion);
			policy.Add(Operations.Streams.Write, streamAssertion);
			policy.Add(Operations.Streams.Delete, streamAssertion);
			policy.Add(Operations.Streams.MetadataRead, streamAssertion);
			policy.Add(Operations.Streams.MetadataWrite, streamAssertion);

			var matchUsername = new ClaimValueMatchesParameterValueAssertion(ClaimTypes.Name,
				Operations.Users.Parameters.UserParameterName, Grant.Allow);
			var isAdmin = new MultipleClaimMatchAssertion(Grant.Allow, MultipleMatchMode.Any, Admins);
			policy.Add(Operations.Users.List, isAdmin);
			policy.Add(Operations.Users.Create, isAdmin);
			policy.Add(Operations.Users.Delete, isAdmin);
			policy.Add(Operations.Users.Update, isAdmin);
			policy.Add(Operations.Users.Enable, isAdmin);
			policy.Add(Operations.Users.Disable, isAdmin);
			policy.Add(Operations.Users.ResetPassword, isAdmin);
			policy.RequireAuthenticated(Operations.Users.CurrentUser);
			policy.Add(Operations.Users.Read, new OrAssertion(isAdmin, matchUsername));
			policy.Add(Operations.Users.ChangePassword, matchUsername);


			policy.RequireAuthenticated(Operations.Projections.List);

			policy.RequireAuthenticated(Operations.Projections.Abort);
			policy.RequireAuthenticated(Operations.Projections.Create);
			policy.RequireAuthenticated(Operations.Projections.DebugProjection);
			policy.AddMatchAnyAssertion(Operations.Projections.Delete, Grant.Allow, OperationsOrAdmins);
			policy.RequireAuthenticated(Operations.Projections.Disable);
			policy.RequireAuthenticated(Operations.Projections.Enable);
			policy.RequireAuthenticated(Operations.Projections.Read);
			policy.AddMatchAnyAssertion(Operations.Projections.ReadConfiguration, Grant.Allow, OperationsOrAdmins);
			policy.RequireAuthenticated(Operations.Projections.Reset);
			policy.RequireAuthenticated(Operations.Projections.Update);
			policy.AddMatchAnyAssertion(Operations.Projections.UpdateConfiguration, Grant.Allow, OperationsOrAdmins);
			policy.RequireAuthenticated(Operations.Projections.State);
			policy.RequireAuthenticated(Operations.Projections.Status);
			policy.RequireAuthenticated(Operations.Projections.Statistics);
			policy.AddMatchAnyAssertion(Operations.Projections.Restart, Grant.Allow, Admins);

			return new PolicyAuthorizationProvider(new PolicyEvaluator(policy.AsReadOnly()),
				Log.ForContext<PolicyEvaluator>(), true, false);
		}
	}
}
