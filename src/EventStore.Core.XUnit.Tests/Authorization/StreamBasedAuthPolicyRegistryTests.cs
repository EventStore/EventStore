// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DotNext.Threading;
using EventStore.Common.Exceptions;
using EventStore.Core.Authorization.AuthorizationPolicies;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Tests;
using EventStore.Core.TransactionLog.LogRecords;
using Xunit;
using EventRecord = EventStore.Core.Data.EventRecord;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Core.XUnit.Tests.Authorization;

public class StreamBasedAuthPolicyRegistryTests {
	private readonly SynchronousScheduler _publisher = new();
	private static AuthorizationPolicySettings WithAclDefault => new(LegacyPolicySelectorFactory.LegacyPolicySelectorName);
	private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

	private StreamBasedAuthorizationPolicyRegistry CreateSut(
		IPolicySelectorFactory[] plugins, AuthorizationPolicySettings defaultSettings, AsyncManualResetEvent policyUpdated) {
		var legacySelector = new LegacyPolicySelectorFactory(false, false, false).Create(_publisher);
		var sut = new StreamBasedAuthorizationPolicyRegistry(_publisher, legacySelector, plugins, defaultSettings);
		sut.PolicyChanged += (_, _) => {
			policyUpdated.Set();
		};
		return sut;
	}

	[Theory]
	[InlineData(LegacyPolicySelectorFactory.LegacyPolicySelectorName, new[]{LegacyPolicySelectorFactory.LegacyPolicySelectorName})]
	[InlineData(FallbackStreamAccessPolicySelector.FallbackPolicyName, new[]{FallbackStreamAccessPolicySelector.FallbackPolicyName})]
	[InlineData("plugin", new[]{"plugin", LegacyPolicySelectorFactory.LegacyPolicySelectorName})]
	public async Task when_starting_with_no_existing_settings_events_the_default_is_used(string defaultPolicy, string[] expected) {
		var subscribed = new TaskCompletionSource<ClientMessage.SubscribeToStream>();
		var policyUpdated = new AsyncManualResetEvent(false);
		_publisher.Subscribe(
			new AdHocHandler<ClientMessage.ReadStreamEventsBackward>(
				m => {
					m.Envelope.ReplyWith(ErrorResponse(m, ReadStreamResult.NoStream));
				}));
		_publisher.Subscribe(new AdHocHandler<ClientMessage.SubscribeToStream>(m => {
			subscribed.TrySetResult(m);
		}));
		var sut = CreateSut(
			[new FakePolicySelectorPlugin("plugin", true)],
			new AuthorizationPolicySettings(defaultPolicy),
			policyUpdated);

		await sut.Start();
		Assert.True(await policyUpdated.WaitAsync(Timeout));

		// It uses the provided default settings
		for (var i = 0; i < expected.Length; i++) {
			Assert.Equal(expected[i], sut.EffectivePolicies[i].Information.Name);
		}
		// It should subscribe
		var subscribeMsg = await subscribed.Task.WithTimeout();
		Assert.Equal(SystemStreams.AuthorizationPolicyRegistryStream, subscribeMsg.EventStreamId);
	}

	[Theory]
	[InlineData(ClientMessage.NotHandled.Types.NotHandledReason.NotReady)]
	[InlineData(ClientMessage.NotHandled.Types.NotHandledReason.TooBusy)]
	public async Task when_starting_and_read_backwards_is_not_handled(ClientMessage.NotHandled.Types.NotHandledReason notHandledReason) {
		var longerTimeout = TimeSpan.FromSeconds(20); // Retries have a delay
		var subscribed = new TaskCompletionSource<ClientMessage.SubscribeToStream>();
		var policyUpdated = new AsyncManualResetEvent(false);
		var failed = false;
		_publisher.Subscribe(
			new AdHocHandler<ClientMessage.ReadStreamEventsBackward>(
				m => {
					if (!failed) {
						failed = true;
						m.Envelope.ReplyWith(new ClientMessage.NotHandled(m.CorrelationId, notHandledReason, ""));
					} else
						m.Envelope.ReplyWith(CreateReadCompleted(m, [
							CreateResolvedEventForAuthSettings(new AuthorizationPolicySettings("plugin"), 0)]));
				}));
		_publisher.Subscribe(new AdHocHandler<ClientMessage.SubscribeToStream>(m => {
			subscribed.TrySetResult(m);
		}));
		var sut = CreateSut(
			[new FakePolicySelectorPlugin("plugin", true)],
			WithAclDefault, policyUpdated);
		await sut.Start();

		// It retries and eventually updates the policy
		Assert.True(await policyUpdated.WaitAsync(longerTimeout));
		Assert.Equal("plugin", sut.EffectivePolicies[0].Information.Name);
		Assert.Equal(LegacyPolicySelectorFactory.LegacyPolicySelectorName, sut.EffectivePolicies[1].Information.Name);
		// It should subscribe
		var subscribeMsg = await subscribed.Task.WithTimeout(longerTimeout);
		Assert.Equal(SystemStreams.AuthorizationPolicyRegistryStream, subscribeMsg.EventStreamId);
	}

	[Theory]
	[InlineData(ReadStreamResult.AccessDenied, true)]
	[InlineData(ReadStreamResult.Error, true)]
	[InlineData(ReadStreamResult.StreamDeleted, false)]
	[InlineData(ReadStreamResult.NoStream, false)]
	public async Task when_starting_and_read_backwards_fails_and_cannot_be_retried(ReadStreamResult readResult, bool fatal) {
		var subscribed = new TaskCompletionSource<ClientMessage.SubscribeToStream>();
		var policyUpdated = new AsyncManualResetEvent(false);
		Exception? actualException = null;
		_publisher.Subscribe(
			new AdHocHandler<ClientMessage.ReadStreamEventsBackward>(
				m => {
					m.Envelope.ReplyWith(ErrorResponse(m, readResult));
				}));
		_publisher.Subscribe(new AdHocHandler<ClientMessage.SubscribeToStream>(m => {
			subscribed.TrySetResult(m);
		}));
		var sut = CreateSut(
			[new FakePolicySelectorPlugin("plugin", true)], WithAclDefault, policyUpdated);
		try {
			await sut.Start();
		} catch (Exception ex) {
			actualException = ex;
		}
		if (fatal) {
			// It throws a fatal exception
			Assert.NotNull(actualException);
			Assert.IsType<ApplicationInitializationException>(actualException);
		} else {
			Assert.Null(actualException);
			// It eventually updates the policy
			Assert.True(await policyUpdated.WaitAsync(Timeout));
			// It subscribes
			var subscribeMsg = await subscribed.Task.WithTimeout();
			Assert.Equal(SystemStreams.AuthorizationPolicyRegistryStream, subscribeMsg.EventStreamId);
		}
	}

	public static IEnumerable<object[]> InvalidAuthorizationPolicySettings =>
		new List<object[]> {
			new object[] { new[]{new AuthPolicyRegistryTestCase(new AuthorizationPolicySettings("not found"), 0)} },
			new object[] { new[]{new AuthPolicyRegistryTestCase(SystemEventTypes.AuthorizationPolicyChanged, "invalid"u8.ToArray(), 0)} },
			new object[] { new[]{new AuthPolicyRegistryTestCase("invalid", JsonSerializer.SerializeToUtf8Bytes(WithAclDefault), 0)} },
			new object[] { new[] {
				new AuthPolicyRegistryTestCase("invalid", JsonSerializer.SerializeToUtf8Bytes(WithAclDefault), 1),
				new AuthPolicyRegistryTestCase("invalid", "invalid"u8.ToArray(), 0)
			} },
		};

	[Fact]
	public void when_starting_and_read_backwards_has_not_completed_yet() {
		var sut = CreateSut([], WithAclDefault, new AsyncManualResetEvent(false));
		// It uses the fallback policy for stream access, and the legacy policy for endpoint access
		Assert.Equal(2, sut.EffectivePolicies.Length);
		Assert.Equal(FallbackStreamAccessPolicySelector.FallbackPolicyName, sut.EffectivePolicies[0].Information.Name);
		Assert.Equal(LegacyPolicySelectorFactory.LegacyPolicySelectorName, sut.EffectivePolicies[1].Information.Name);
	}

	[Theory]
	[MemberData(nameof(InvalidAuthorizationPolicySettings))]
	public async Task when_starting_and_all_existing_settings_events_are_invalid(AuthPolicyRegistryTestCase[] readResult) {
		var policyUpdated = new AsyncManualResetEvent(false);
		_publisher.Subscribe(
			new AdHocHandler<ClientMessage.ReadStreamEventsBackward>(
				m => {
					m.Envelope.ReplyWith(CreateReadCompleted(m, readResult.Select(x => x.ResolvedEvent).ToArray()));
				}));
		var sut = CreateSut([], WithAclDefault, policyUpdated);
		await sut.Start();
		// It uses the fallback policy for stream access, and the legacy policy for endpoint access
		Assert.True(await policyUpdated.WaitAsync(Timeout));
		Assert.Equal(2, sut.EffectivePolicies.Length);
		Assert.Equal(FallbackStreamAccessPolicySelector.FallbackPolicyName, sut.EffectivePolicies[0].Information.Name);
		Assert.Equal(LegacyPolicySelectorFactory.LegacyPolicySelectorName, sut.EffectivePolicies[1].Information.Name);
	}

	// Start from event number 1 in case the test includes a default event
	public static IEnumerable<object[]> SomeValidAuthorizationPolicySettings =>
		new List<object[]> {
			new object[] { new[] {
				new AuthPolicyRegistryTestCase(new AuthorizationPolicySettings("plugin"), 1)}
			},
			new object[] { new[]{
				new AuthPolicyRegistryTestCase(new AuthorizationPolicySettings("not found"), 1),
				new AuthPolicyRegistryTestCase(new AuthorizationPolicySettings("plugin"), 2)}
			},
			new object[] { new[]{
				new AuthPolicyRegistryTestCase(new AuthorizationPolicySettings("plugin"), 1),
				new AuthPolicyRegistryTestCase(SystemEventTypes.AuthorizationPolicyChanged, "invalid"u8.ToArray(), 2)}
			},
			new object[] { new[]{
				new AuthPolicyRegistryTestCase(new AuthorizationPolicySettings("plugin"), 1),
				new AuthPolicyRegistryTestCase(new AuthorizationPolicySettings("disabled"), 2)}
			}
		};

	[Theory]
	[MemberData(nameof(SomeValidAuthorizationPolicySettings))]
	public async Task when_starting_and_some_existing_settings_events_are_valid(AuthPolicyRegistryTestCase[] policyEvents) {
		var policyUpdated = new AsyncManualResetEvent(false);
		_publisher.Subscribe(
			new AdHocHandler<ClientMessage.ReadStreamEventsBackward>(
				m => {
					// Reverse the events for backwards read
					m.Envelope.ReplyWith(CreateReadCompleted(m, policyEvents.Reverse().Select(x => x.ResolvedEvent).ToArray()));
				}));
		var sut = CreateSut([
			new FakePolicySelectorPlugin("plugin", true),
			new FakePolicySelectorPlugin("disabled", false)
		], WithAclDefault, policyUpdated);
		await sut.Start();

		// It uses the expected policies
		Assert.True(await policyUpdated.WaitAsync(Timeout));
		Assert.Equal("plugin", sut.EffectivePolicies[0].Information.Name);
		Assert.Equal(LegacyPolicySelectorFactory.LegacyPolicySelectorName, sut.EffectivePolicies[1].Information.Name);
	}

	[Theory]
	[MemberData(nameof(SomeValidAuthorizationPolicySettings))]
	public async Task when_settings_events_are_written_after_startup(AuthPolicyRegistryTestCase[] testCases) {
		var subscribed = new TaskCompletionSource<ClientMessage.SubscribeToStream>();
		var onEnabled = new AsyncCountdownEvent(1);
		var policyUpdated = new AsyncManualResetEvent(false);
		_publisher.Subscribe(
			new AdHocHandler<ClientMessage.ReadStreamEventsBackward>(
				m => {
					m.Envelope.ReplyWith(CreateReadCompleted(m,
						[CreateResolvedEventForAuthSettings(WithAclDefault, 0)]));
				}));
		_publisher.Subscribe(new AdHocHandler<ClientMessage.SubscribeToStream>(
			m => {
				m.Envelope.ReplyWith(new ClientMessage.SubscriptionConfirmation(m.CorrelationId, 100, 0));
				subscribed.TrySetResult(m);
			}));

		var sut = CreateSut([
			new FakePolicySelectorPlugin("plugin", true, onEnabled),
			new FakePolicySelectorPlugin("disabled", false)
		], WithAclDefault, policyUpdated);
		await sut.Start();

		// It uses the default acls
		Assert.True(await policyUpdated.WaitAsync(Timeout));
		policyUpdated.Reset();
		Assert.Equal(LegacyPolicySelectorFactory.LegacyPolicySelectorName, sut.EffectivePolicies[0].Information.Name);

		// Send the updated settings events
		var subscribeMsg = await subscribed.Task.WithTimeout();
		foreach (var testCase in testCases) {
			subscribeMsg.Envelope.ReplyWith(
				new ClientMessage.StreamEventAppeared(subscribeMsg.CorrelationId, testCase.ResolvedEvent));
		}

		// The valid policy is enabled
		Assert.True(await onEnabled.WaitAsync(Timeout));
		// The policy is updated
		Assert.True(await policyUpdated.WaitAsync(Timeout));
		// The correct policies are in use
		Assert.Equal("plugin", sut.EffectivePolicies[0].Information.Name);
		Assert.Equal(LegacyPolicySelectorFactory.LegacyPolicySelectorName, sut.EffectivePolicies[1].Information.Name);
	}

	[Fact]
	public async Task when_a_new_plugin_is_enabled() {
		var subscribed = new TaskCompletionSource<ClientMessage.SubscribeToStream>();
		var onEnabled = new AsyncCountdownEvent(2);
		var onDisabled = new AsyncCountdownEvent(1);
		var policyUpdated = new AsyncManualResetEvent(false);
		_publisher.Subscribe(
			new AdHocHandler<ClientMessage.ReadStreamEventsBackward>(
				m => {
					m.Envelope.ReplyWith(CreateReadCompleted(m, [
						CreateResolvedEventForAuthSettings(new AuthorizationPolicySettings("plugin1"), 0)]));
				}));
		_publisher.Subscribe(new AdHocHandler<ClientMessage.SubscribeToStream>(
			m => {
				m.Envelope.ReplyWith(new ClientMessage.SubscriptionConfirmation(m.CorrelationId, 100, 0));
				subscribed.TrySetResult(m);
			}));

		var sut = CreateSut([
			new FakePolicySelectorPlugin("plugin1", true, onEnabled, onDisabled),
			new FakePolicySelectorPlugin("plugin2", true, onEnabled)
		], WithAclDefault, policyUpdated);
		await sut.Start();
		Assert.True(await policyUpdated.WaitAsync(Timeout));
		policyUpdated.Reset();

		// It uses the first plugin
		Assert.Equal("plugin1", sut.EffectivePolicies[0].Information.Name);
		Assert.Equal(1, onEnabled.CurrentCount);

		// Send the updated settings events
		var subscribeMsg = await subscribed.Task.WithTimeout();
		subscribeMsg.Envelope.ReplyWith(
			new ClientMessage.StreamEventAppeared(subscribeMsg.CorrelationId,
				CreateResolvedEventForAuthSettings(new AuthorizationPolicySettings("plugin2"), 1)));

		// The policies are enabled
		Assert.True(await onEnabled.WaitAsync(Timeout));
		Assert.Equal(0, onEnabled.CurrentCount);

		// The previous policy is disabled
		Assert.True(await onDisabled.WaitAsync(Timeout));

		// The correct policies are in use
		Assert.True(await policyUpdated.WaitAsync(Timeout));
		Assert.Equal("plugin2", sut.EffectivePolicies[0].Information.Name);
		Assert.Equal(LegacyPolicySelectorFactory.LegacyPolicySelectorName, sut.EffectivePolicies[1].Information.Name);
	}

	private static ClientMessage.ReadStreamEventsBackwardCompleted
		ErrorResponse(ClientMessage.ReadStreamEventsBackward msg, ReadStreamResult result) =>
		new(
			msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount,
			result, [], StreamMetadata.Empty, true, "",
			msg.FromEventNumber, msg.FromEventNumber, true, 0L);

	private static ResolvedEvent CreateResolvedEventForAuthSettings(AuthorizationPolicySettings settings, long eventNumber) {
		var data = JsonSerializer.SerializeToUtf8Bytes(settings, SerializerOptions);
		return CreateResolvedEvent(SystemEventTypes.AuthorizationPolicyChanged, data, eventNumber);
	}

	private static ResolvedEvent CreateResolvedEvent(string eventType, byte[] data, long eventNumber) {
		var logPosition = eventNumber * 100;
		var prepare = new PrepareLogRecord(logPosition, Guid.NewGuid(), Guid.NewGuid(),
			transactionPosition: logPosition,
			transactionOffset: 0,
			SystemStreams.AuthorizationPolicyRegistryStream,
			eventStreamIdSize: null,
			expectedVersion: eventNumber, DateTime.Now, PrepareFlags.Data,
			eventType, eventTypeSize: null, data, ReadOnlyMemory<byte>.Empty);
		var eventRecord = new EventRecord(eventNumber, prepare, SystemStreams.AuthorizationPolicyRegistryStream, eventType);
		return ResolvedEvent.ForUnresolvedEvent(eventRecord, logPosition);
	}

	private ClientMessage.ReadStreamEventsBackwardCompleted CreateReadCompleted(
		ClientMessage.ReadStreamEventsBackward msg, ResolvedEvent[] events) =>
		new(msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount,
			ReadStreamResult.Success, events, StreamMetadata.Empty, true, "",
			msg.FromEventNumber, msg.FromEventNumber, true, 1000L);

	public readonly struct AuthPolicyRegistryTestCase {
		public readonly ResolvedEvent ResolvedEvent;
		private readonly string _description;

		public AuthPolicyRegistryTestCase(AuthorizationPolicySettings settings, long eventNumber) {
			ResolvedEvent = CreateResolvedEventForAuthSettings(settings, eventNumber);
			_description = $"{eventNumber}:{ResolvedEvent.Event.EventType}+{settings}";
		}
		public AuthPolicyRegistryTestCase(string eventType, byte[] data, long eventNumber) {
			ResolvedEvent = CreateResolvedEvent(eventType, data, eventNumber);
			_description = $"{eventNumber}:{eventType}+{Encoding.UTF8.GetString(data)}";
		}

		public override string ToString() => _description;
	}
}
