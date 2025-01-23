// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Authorization;
using EventStore.Core.Authorization.AuthorizationPolicies;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using EventStore.Plugins.Authorization;
using NUnit.Framework;
using static EventStore.Core.Data.SystemSettings;
using static EventStore.Core.Messages.StorageMessage;
using static EventStore.Plugins.Authorization.Operations;

namespace EventStore.Core.Tests.Authorization;

[TestFixture(true, true, true)]
[TestFixture(true, true, false)]
[TestFixture(true, false, true)]
[TestFixture(true, false, false)]
[TestFixture(false, true, true)]
[TestFixture(false, true, false)]
[TestFixture(false, false, true)]
[TestFixture(false, false, false)]
public class LegacyPolicyVerification {
	private const string StreamWithDefaultPermissions = "StreamWithDefaultPermissions";
	private const string StreamWithCustomPermissions = "StreamWithCustomPermissions";

	private readonly IAuthorizationProvider _authorizationProvider;
	private readonly AclResponder _aclResponder;
	private static bool _allowAnonymousEndpointAccess;
	private static bool _allowAnonymousStreamAccess;
	private static bool _overrideAnonymousGossipEndpointAccess;

	public LegacyPolicyVerification(bool allowAnonymousEndpointAccess, bool allowAnonymousStreamAccess, bool overrideAnonymousGossipEndpointAccess) {
		_aclResponder = new AclResponder();
		_allowAnonymousEndpointAccess = allowAnonymousEndpointAccess;
		_allowAnonymousStreamAccess = allowAnonymousStreamAccess;
		_overrideAnonymousGossipEndpointAccess = overrideAnonymousGossipEndpointAccess;
		_authorizationProvider = new InternalAuthorizationProviderFactory(
				new StaticAuthorizationPolicyRegistry([
					new LegacyPolicySelectorFactory(
						allowAnonymousEndpointAccess,
						allowAnonymousStreamAccess,
						overrideAnonymousGossipEndpointAccess).Create(_aclResponder)
				]))
			.Build();
	}

	public abstract class PolicyVerificationParameters(ClaimsPrincipal user, Operation operation, string stream, EffectiveAcl streamAcl) {
		public ClaimsPrincipal User { get; } = user;
		public Operation Operation { get; } = operation;
		public string Stream { get; } = stream;
		public EffectiveAcl StreamAcl { get; } = streamAcl;
		public abstract string TestName { get; }

		public override string ToString() => TestName;
	}

	public class StaticPolicyVerificationParameters(ClaimsPrincipal user, Operation operation, string stream, EffectiveAcl streamAcl, bool isAuthorized, bool shouldRequestAcl)
		: PolicyVerificationParameters(user, operation, stream, streamAcl) {
		public bool IsAuthorized { get; } = isAuthorized;
		public bool ShouldRequestAcl { get; } = shouldRequestAcl;
		public override string TestName => $"{User.Identity?.Name ?? "Anonymous (empty)"} {(IsAuthorized ? "is" : "is not")} authorized to perform operation {Operation}";
	}

	public class ConfigurablePolicyVerificationParameters(
		ClaimsPrincipal user,
		Operation operation,
		string stream,
		EffectiveAcl streamAcl,
		Func<bool, bool, bool> authorizationCheck,
		Func<bool, bool> aclCheck)
		: PolicyVerificationParameters(user, operation, stream, streamAcl) {
		public Func<bool, bool, bool> AuthorizationCheck { get; } = authorizationCheck;
		public Func<bool, bool> AclCheck { get; } = aclCheck;
		public override string TestName => $"Verify if Anonymous user is authorized to perform operation {Operation}";
	}

	public class GossipPolicyVerificationParameters(
		ClaimsPrincipal user,
		Operation operation,
		string stream,
		EffectiveAcl streamAcl,
		Func<bool, bool, bool> authorizationCheck)
		: PolicyVerificationParameters(user, operation, stream, streamAcl) {
		public Func<bool, bool, bool> AuthorizationCheck { get; } = authorizationCheck;

		public override string TestName => $"Verify if Anonymous user is authorized to perform gossip operation {Operation}";
	}

	public static IEnumerable<PolicyVerificationParameters> PolicyTests() {
		var systemStreamPermission = new EffectiveAcl(Default.SystemStreamAcl, Default.SystemStreamAcl, Default.SystemStreamAcl);
		var defaultUseruserStreamPermission = new EffectiveAcl(Default.UserStreamAcl, Default.UserStreamAcl, Default.UserStreamAcl);
		var userStreamPermission = new EffectiveAcl(new("test", "test", "test", "test", "test"), Default.UserStreamAcl, Default.UserStreamAcl);

		var admin = CreatePrincipal("admin", SystemRoles.Admins);
		var userAdmin = CreatePrincipal("adminuser", SystemRoles.Admins);
		var ops = CreatePrincipal("ops", SystemRoles.Operations);
		var userOps = CreatePrincipal("opsuser", SystemRoles.Operations);
		var user1 = CreatePrincipal("test");
		var user2 = CreatePrincipal("test2");
		var userSystem = SystemAccounts.System;

		var admins = new[] { admin, userAdmin };
		var operations = new[] { ops, userOps };
		var users = new[] { user1, user2 };
		var system = new[] { userSystem };
		var anonymous = new[] { new ClaimsPrincipal(), new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Anonymous, "")])), };
		foreach (var user in system) {
			foreach (var operation in SystemOperations()) {
				yield return new StaticPolicyVerificationParameters(user,
					operation.Item1, operation.Item2, operation.Item3,
					true,
					false
				);
			}
		}

		foreach (var user in admins) {
			foreach (var operation in SystemOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, false);
			}

			foreach (var operation in AdminOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, false);
			}

			foreach (var operation in OpsOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, false);
			}

			foreach (var operation in UserOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, false);
			}

			foreach (var operation in AuthenticatedOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, false);
			}

			foreach (var operation in AnonymousOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, false);
			}
		}

		foreach (var user in operations) {
			foreach (var operation in SystemOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, false);
			}

			foreach (var operation in AdminOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, operation.Item3 != null);
			}

			foreach (var operation in OpsOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, false);
			}

			foreach (var operation in UserOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3,
					operation.Item2 == null || operation.Item3 == defaultUseruserStreamPermission, operation.Item2 != null);
			}

			foreach (var operation in AuthenticatedOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, operation.Item2 != null);
			}

			foreach (var operation in AnonymousOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, operation.Item2 != null);
			}
		}

		foreach (var user in users) {
			foreach (var operation in SystemOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, false);
			}

			foreach (var operation in AdminOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, operation.Item3 != null);
			}

			foreach (var operation in OpsOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, false);
			}

			foreach (var operation in UserOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3,
					operation.Item2 == null || user.Identity.Name != "test2" || operation.Item3 == defaultUseruserStreamPermission, operation.Item3 != null);
			}

			foreach (var operation in AuthenticatedOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, operation.Item3 != null);
			}

			foreach (var operation in AnonymousOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, operation.Item3 != null);
			}
		}

		foreach (var user in anonymous) {
			foreach (var operation in SystemOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, false);
			}

			foreach (var operation in AdminOperations()) {
				yield return new ConfigurablePolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, (_, _) => false,
					allowAnonymousStreamAccess => allowAnonymousStreamAccess);
			}

			foreach (var operation in OpsOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, operation.Item3 != null);
			}

			foreach (var operation in UserOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, false);
			}

			foreach (var operation in AuthenticatedOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, false, false);
			}

			foreach (var operation in AnonymousOperations()) {
				yield return new StaticPolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, true, operation.Item3 != null);
			}

			foreach (var operation in AllowAnonymousEndpointAccessOperations()) {
				yield return new ConfigurablePolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, (allowAnonymousEndpointAccess, _) => allowAnonymousEndpointAccess,
					_ => operation.Item3 != null
				);
			}

			foreach (var operation in AllowAnonymousStreamAccessOperations()) {
				yield return new ConfigurablePolicyVerificationParameters(user, operation.Item1, operation.Item2, operation.Item3, (_, allowAnonymousStreamAccess) => allowAnonymousStreamAccess,
					allowAnonymousStreamAccess => allowAnonymousStreamAccess);
			}

			foreach (var operation in ClientGossipOperations()) {
				yield return new GossipPolicyVerificationParameters(user,
					operation.Item1, operation.Item2, operation.Item3,
					(allowAnonymousGossipAccess, overrideAnonymousGossipAccess) =>
						overrideAnonymousGossipAccess || allowAnonymousGossipAccess
				);
			}
		}

		yield break;

		IEnumerable<(Operation, string, EffectiveAcl)> SystemOperations() {
			yield return CreateOperation(Node.Gossip.Update);
			yield return CreateOperation(Node.Elections.Prepare);
			yield return CreateOperation(Node.Elections.PrepareOk);
			yield return CreateOperation(Node.Elections.ViewChange);
			yield return CreateOperation(Node.Elections.ViewChangeProof);
			yield return CreateOperation(Node.Elections.Proposal);
			yield return CreateOperation(Node.Elections.Accept);
			yield return CreateOperation(Node.Elections.LeaderIsResigning);
			yield return CreateOperation(Node.Elections.LeaderIsResigningOk);
			yield return CreateOperation(Node.Gossip.Read);
		}

		IEnumerable<(Operation, string, EffectiveAcl)> AdminOperations() {
			yield return (
				new Operation(Streams.Read).WithParameter(Streams.Parameters.StreamId("$$$scavenge")),
				"$$$scavenge",
				systemStreamPermission);
		}

		IEnumerable<(Operation, string, EffectiveAcl)> OpsOperations() {
			yield return CreateOperation(Node.Information.Subsystems);

			yield return CreateOperation(Node.Shutdown);
			yield return CreateOperation(Node.ReloadConfiguration);
			yield return CreateOperation(Node.Scavenge.Start);
			yield return CreateOperation(Node.Scavenge.Stop);
			yield return CreateOperation(Node.Scavenge.Read);
			yield return CreateOperation(Node.MergeIndexes);
			yield return CreateOperation(Node.SetPriority);
			yield return CreateOperation(Node.Resign);

			yield return CreateOperation(Subscriptions.Create);
			yield return CreateOperation(Subscriptions.Update);
			yield return CreateOperation(Subscriptions.Delete);
			yield return CreateOperation(Subscriptions.Restart);

			yield return CreateOperation(Node.Information.Histogram);
			yield return CreateOperation(Node.Information.Options);
			yield return (new Operation(Subscriptions.ReplayParked).WithParameter(Subscriptions.Parameters.StreamId(StreamWithCustomPermissions)), StreamWithCustomPermissions, null);
			yield return (new Operation(Subscriptions.ReplayParked).WithParameter(Subscriptions.Parameters.StreamId(StreamWithDefaultPermissions)), StreamWithDefaultPermissions, null);

			yield return CreateOperation(Projections.UpdateConfiguration);

			yield return CreateOperation(Projections.ReadConfiguration);
			yield return CreateOperation(Projections.Delete);
			yield return CreateOperation(Projections.Restart);
		}

		IEnumerable<(Operation, string, EffectiveAcl)> UserOperations() {
			yield return (new Operation(Subscriptions.ProcessMessages).WithParameter(Subscriptions.Parameters.StreamId(StreamWithCustomPermissions)),
				StreamWithCustomPermissions, userStreamPermission);
			yield return (new Operation(Subscriptions.ProcessMessages).WithParameter(Subscriptions.Parameters.StreamId(StreamWithDefaultPermissions)),
				StreamWithDefaultPermissions, defaultUseruserStreamPermission);
			yield return CreateOperation(Projections.List);
			yield return CreateOperation(Projections.Abort);
			yield return CreateOperation(Projections.Create);
			yield return CreateOperation(Projections.DebugProjection);
			yield return CreateOperation(Projections.Disable);
			yield return CreateOperation(Projections.Enable);
			yield return CreateOperation(Projections.Read);
			yield return CreateOperation(Projections.Reset);
			yield return CreateOperation(Projections.Update);
			yield return CreateOperation(Projections.State);
			yield return CreateOperation(Projections.Status);
			yield return CreateOperation(Projections.Statistics);
		}

		IEnumerable<(Operation, string, EffectiveAcl)> AuthenticatedOperations() {
			yield return CreateOperation(Subscriptions.Statistics);
			yield return CreateOperation(Projections.List);
		}

		IEnumerable<(Operation, string, EffectiveAcl)> AnonymousOperations() {
			yield return CreateOperation(Node.Redirect);
			yield return CreateOperation(Node.StaticContent);
			yield return CreateOperation(Node.Ping);
			yield return CreateOperation(Node.Information.Read);
		}

		IEnumerable<(Operation, string, EffectiveAcl)> ClientGossipOperations() {
			yield return CreateOperation(Node.Gossip.ClientRead);
		}

		IEnumerable<(Operation, string, EffectiveAcl)> AllowAnonymousEndpointAccessOperations() {
			yield return CreateOperation(Node.Options);
			yield return CreateOperation(Node.Statistics.Read);
			yield return CreateOperation(Node.Statistics.Replication);
			yield return CreateOperation(Node.Statistics.Tcp);
			yield return CreateOperation(Node.Statistics.Custom);
		}

		IEnumerable<(Operation, string, EffectiveAcl)> AllowAnonymousStreamAccessOperations() {
			yield return (new Operation(Streams.Read).WithParameter(Streams.Parameters.StreamId(StreamWithDefaultPermissions)), StreamWithDefaultPermissions, defaultUseruserStreamPermission);
			yield return (new Operation(Streams.Write).WithParameter(Streams.Parameters.StreamId(StreamWithDefaultPermissions)), StreamWithDefaultPermissions, defaultUseruserStreamPermission);
			yield return (new Operation(Streams.Delete).WithParameter(Streams.Parameters.StreamId(StreamWithDefaultPermissions)), StreamWithDefaultPermissions, defaultUseruserStreamPermission);
			yield return (new Operation(Streams.MetadataRead).WithParameter(Streams.Parameters.StreamId(StreamWithDefaultPermissions)), StreamWithDefaultPermissions, defaultUseruserStreamPermission);
			yield return (new Operation(Streams.MetadataWrite).WithParameter(Streams.Parameters.StreamId(StreamWithDefaultPermissions)), StreamWithDefaultPermissions, defaultUseruserStreamPermission);
		}

		(Operation, string, EffectiveAcl) CreateOperation(OperationDefinition def) {
			return (new Operation(def), null, null);
		}

		ClaimsPrincipal CreatePrincipal(string name, params string[] roles) {
			var claims = new[] { new Claim(ClaimTypes.Name, name) }.Concat(roles.Select(x => new Claim(ClaimTypes.Role, x)));
			return new ClaimsPrincipal(new ClaimsIdentity(claims));
		}
	}

	[Test]
	public async Task VerifyPolicy([ValueSource(nameof(PolicyTests))] PolicyVerificationParameters pvp) {
		_aclResponder.ExpectedAcl(pvp.Stream, pvp.StreamAcl);
		var result = await _authorizationProvider.CheckAccessAsync(pvp.User, pvp.Operation, CancellationToken.None);
		switch (pvp) {
			case StaticPolicyVerificationParameters staticPvp:
				Assert.AreEqual(staticPvp.IsAuthorized, result, staticPvp.IsAuthorized ? "was not authorized" : "was authorized");
				Assert.AreEqual(staticPvp.ShouldRequestAcl, _aclResponder.MessageReceived, staticPvp.ShouldRequestAcl ? "did not request acl" : "requested acl");
				break;
			case ConfigurablePolicyVerificationParameters confPvp:
				Assert.AreEqual(confPvp.AuthorizationCheck(_allowAnonymousEndpointAccess, _allowAnonymousStreamAccess), result,
					confPvp.AuthorizationCheck(_allowAnonymousEndpointAccess, _allowAnonymousStreamAccess)
						? "was not authorized"
						: "was authorized");
				Assert.AreEqual(confPvp.AclCheck(_allowAnonymousStreamAccess), _aclResponder.MessageReceived,
					confPvp.AclCheck(_allowAnonymousStreamAccess) ? "did not request acl" : "requested acl");
				break;
			case GossipPolicyVerificationParameters gossipPvp:
				Assert.AreEqual(gossipPvp.AuthorizationCheck(_allowAnonymousEndpointAccess, _overrideAnonymousGossipEndpointAccess), result,
					gossipPvp.AuthorizationCheck(_allowAnonymousEndpointAccess, _overrideAnonymousGossipEndpointAccess)
						? "was not authorized"
						: "was authorized");
				break;
		}
	}

	class AclResponder : IPublisher {
		public bool MessageReceived { get; private set; } = false;
		private EffectiveAcl _acl;
		private string _expectedStream;

		public void Publish(Message message) {
			MessageReceived = true;
			Assert.IsInstanceOf<EffectiveStreamAclRequest>(message);
			var request = (EffectiveStreamAclRequest)message;
			Assert.AreEqual(_expectedStream, request.StreamId);
			Assert.NotNull(request.Envelope);
			request.Envelope.ReplyWith(new EffectiveStreamAclResponse(_acl));
		}

		public void ExpectedAcl(string stream, EffectiveAcl acl) {
			MessageReceived = false;
			if (stream == null) return;
			_expectedStream = SystemStreams.IsMetastream(stream) ? SystemStreams.OriginalStreamOf(stream) : stream;
			_acl = acl;
		}
	}
}
