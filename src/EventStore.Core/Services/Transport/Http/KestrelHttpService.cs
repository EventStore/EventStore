// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Settings;
using EventStore.Transport.Http.EntityManagement;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Http;

public class KestrelHttpService(
	ServiceAccessibility accessibility,
	IPublisher inputBus,
	IUriRouter uriRouter,
	bool logHttpRequests,
	string advertiseAsHost,
	int advertiseAsPort,
	params EndPoint[] endPoints)
	: IHttpService, IHandle<SystemMessage.SystemInit>, IHandle<SystemMessage.BecomeShuttingDown> {
	public ServiceAccessibility Accessibility => accessibility;
	public bool IsListening => _isListening;
	public IEnumerable<EndPoint> EndPoints { get; } = Ensure.NotNull(endPoints);

	public IEnumerable<ControllerAction> Actions => UriRouter.Actions;

	private readonly IPublisher _inputBus = Ensure.NotNull(inputBus);
	public IUriRouter UriRouter { get; } = Ensure.NotNull(uriRouter);
	public bool LogHttpRequests { get; } = logHttpRequests;
	public string AdvertiseAsHost { get; } = advertiseAsHost;
	public int AdvertiseAsPort { get; } = advertiseAsPort;

	private bool _isListening;

	public void Handle(SystemMessage.SystemInit message) {
		_isListening = true;
		_inputBus.Publish(new SystemMessage.ServiceInitialized(nameof(KestrelHttpService)));
	}

	public void Handle(SystemMessage.BecomeShuttingDown message) {
		if (message.ShutdownHttp)
			Shutdown();
		_inputBus.Publish(new SystemMessage.ServiceShutdown(nameof(KestrelHttpService), $"[{string.Join(", ", EndPoints)}]"));
	}

	public void Shutdown() {
		_isListening = false;
	}

	public void SetupController(IHttpController controller) {
		Ensure.NotNull(controller);
		controller.Subscribe(this);
	}

	public void RegisterCustomAction(ControllerAction action, Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler) {
		Ensure.NotNull(action);
		Ensure.NotNull(handler);

		UriRouter.RegisterAction(action, handler);
	}

	public void RegisterAction(ControllerAction action, Action<HttpEntityManager, UriTemplateMatch> handler) {
		Ensure.NotNull(action);
		Ensure.NotNull(handler);

		UriRouter.RegisterAction(action, (man, match) => {
			handler(man, match);
			return new RequestParams(ESConsts.HttpTimeout);
		});
	}

	public List<UriToActionMatch> GetAllUriMatches(Uri uri) => UriRouter.GetAllUriMatches(uri);

	public static void CreateAndSubscribePipeline(ISubscriber bus) {
		var requestProcessor = new AuthenticatedHttpRequestProcessor();
		bus.Subscribe<AuthenticatedHttpRequestMessage>(requestProcessor);
	}
}
