// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Bus;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using static EventStore.Core.Messages.UserManagementMessage;
using static EventStore.Plugins.Authorization.Operations;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public class UsersController(IHttpForwarder httpForwarder, IPublisher publisher, IPublisher networkSendQueue) : CommunicationController(publisher) {
	private static readonly ICodec[] DefaultCodecs = [Codec.Json, Codec.Xml];
	private static readonly ILogger Log = Serilog.Log.ForContext<UsersController>();

	protected override void SubscribeCore(IUriRouter router) {
		RegisterUrlBased(router, "/users", HttpMethod.Get, new Operation(Users.List), GetUsers);
		RegisterUrlBased(router, "/users/", HttpMethod.Get, new Operation(Users.List), GetUsers);
		RegisterUrlBased(router, "/users/{login}", HttpMethod.Get, new Operation(Users.Read), GetUser);
		RegisterUrlBased(router, "/users/$current", HttpMethod.Get, new Operation(Users.CurrentUser), GetCurrentUser);
		Register(router, "/users", HttpMethod.Post, PostUser, DefaultCodecs, DefaultCodecs, new Operation(Users.Create));
		Register(router, "/users/", HttpMethod.Post, PostUser, DefaultCodecs, DefaultCodecs, new Operation(Users.Create));
		Register(router, "/users/{login}", HttpMethod.Put, PutUser, DefaultCodecs, DefaultCodecs, new Operation(Users.Update));
		RegisterUrlBased(router, "/users/{login}", HttpMethod.Delete, new Operation(Users.Delete), DeleteUser);
		RegisterUrlBased(router, "/users/{login}/command/enable", HttpMethod.Post, new Operation(Users.Enable), PostCommandEnable);
		RegisterUrlBased(router, "/users/{login}/command/disable", HttpMethod.Post, new Operation(Users.Disable), PostCommandDisable);
		Register(router, "/users/{login}/command/reset-password", HttpMethod.Post, PostCommandResetPassword, DefaultCodecs, DefaultCodecs, new Operation(Users.ResetPassword));
		Register(router, "/users/{login}/command/change-password", HttpMethod.Post, PostCommandChangePassword, DefaultCodecs, DefaultCodecs, ForUser(Users.ChangePassword));
	}

	private static Func<UriTemplateMatch, Operation> ForUser(OperationDefinition definition) {
		return match => new Operation(definition).WithParameter(Users.Parameters.User(match.BoundVariables["login"]));
	}

	private void GetUsers(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;

		var envelope = CreateSendToHttpWithConversionEnvelope(http, (AllUserDetailsResult msg) => new AllUserDetailsResultHttpFormatted(msg, s => MakeUrl(http, s)));

		var message = new GetAll(envelope, http.User);
		Publish(message);
	}

	private void GetUser(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;

		var envelope = CreateSendToHttpWithConversionEnvelope(http, (UserDetailsResult msg) => new UserDetailsResultHttpFormatted(msg, s => MakeUrl(http, s)));

		var login = match.BoundVariables["login"];
		var message = new Get(envelope, http.User, login);
		Publish(message);
	}

	private void GetCurrentUser(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = CreateReplyEnvelope<UserDetailsResult>(http);
		if (http.User == null) {
			envelope.ReplyWith(new UserDetailsResult(Error.Unauthorized));
			return;
		}

		var message = new Get(envelope, http.User, http.User.Identity.Name);
		Publish(message);
	}

	private void PostUser(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = CreateReplyEnvelope<UpdateResult>(
			http, configurator: (codec, result) => {
				var configuration = AutoConfigurator(codec, result);
				return configuration.Code == HttpStatusCode.OK
					? configuration.SetCreated(MakeUrl(http, $"/users/{Uri.EscapeDataString(result.LoginName)}"))
					: configuration;
			});
		http.ReadTextRequestAsync(
			(o, s) => {
				var data = http.RequestCodec.From<PostUserData>(s);
				var message = new Create(envelope, http.User, data.LoginName, data.FullName, data.Groups, data.Password);
				Publish(message);
			}, x => Log.Debug(x, "Reply Text Content Failed."));
	}

	private void PutUser(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = CreateReplyEnvelope<UpdateResult>(http);
		http.ReadTextRequestAsync(
			(_, s) => {
				var login = match.BoundVariables["login"];
				var data = http.RequestCodec.From<PutUserData>(s);
				var message = new Update(envelope, http.User, login, data.FullName, data.Groups);
				Publish(message);
			}, x => Log.Debug(x, "Reply Text Content Failed."));
	}

	private void DeleteUser(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = CreateReplyEnvelope<UpdateResult>(http);
		var login = match.BoundVariables["login"];
		var message = new Delete(envelope, http.User, login);
		Publish(message);
	}

	private void PostCommandEnable(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = CreateReplyEnvelope<UpdateResult>(http);
		var login = match.BoundVariables["login"];
		var message = new Enable(envelope, http.User, login);
		Publish(message);
	}

	private void PostCommandDisable(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = CreateReplyEnvelope<UpdateResult>(http);
		var login = match.BoundVariables["login"];
		var message = new Disable(envelope, http.User, login);
		Publish(message);
	}

	private void PostCommandResetPassword(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = CreateReplyEnvelope<UpdateResult>(http);
		http.ReadTextRequestAsync(
			(_, s) => {
				var login = match.BoundVariables["login"];
				var data = http.RequestCodec.From<ResetPasswordData>(s);
				var message = new ResetPassword(envelope, http.User, login, data.NewPassword);
				Publish(message);
			}, x => Log.Debug(x, "Reply Text Content Failed."));
	}

	private void PostCommandChangePassword(HttpEntityManager http, UriTemplateMatch match) {
		if (httpForwarder.ForwardRequest(http))
			return;
		var envelope = CreateReplyEnvelope<UpdateResult>(http);
		http.ReadTextRequestAsync(
			(_, s) => {
				var login = match.BoundVariables["login"];
				var data = http.RequestCodec.From<ChangePasswordData>(s);
				var message = new ChangePassword(
					envelope, http.User, login, data.CurrentPassword, data.NewPassword);
				Publish(message);
			},
			x => Log.Debug(x, "Reply Text Content Failed."));
	}

	private SendToHttpEnvelope<T> CreateReplyEnvelope<T>(HttpEntityManager http, Func<ICodec, T, string> formatter = null,
		Func<ICodec, T, ResponseConfiguration> configurator = null)
		where T : ResponseMessage {
		return new(networkSendQueue, http, formatter ?? AutoFormatter, configurator ?? AutoConfigurator, null);
	}

	private SendToHttpWithConversionEnvelope<T, R> CreateSendToHttpWithConversionEnvelope<T, R>(
		HttpEntityManager http, Func<T, R> formatter)
		where T : ResponseMessage
		where R : ResponseMessage {
		return new(networkSendQueue,
			http,
			(codec, msg) => codec.To(msg),
			(codec, transformed) => transformed.Success
				? new(HttpStatusCode.OK, codec.ContentType, codec.Encoding)
				: new ResponseConfiguration(ErrorToHttpStatusCode(transformed.Error), codec.ContentType, codec.Encoding),
			formatter);
	}

	private ResponseConfiguration AutoConfigurator<T>(ICodec codec, T result)
		where T : ResponseMessage {
		return result.Success
			? new ResponseConfiguration(HttpStatusCode.OK, codec.ContentType, codec.Encoding)
			: new(ErrorToHttpStatusCode(result.Error), codec.ContentType, codec.Encoding);
	}

	private static string AutoFormatter<T>(ICodec codec, T result) => codec.To(result);

	private static int ErrorToHttpStatusCode(Error error) {
		return error switch {
			Error.Success => HttpStatusCode.OK,
			Error.Conflict => HttpStatusCode.Conflict,
			Error.NotFound => HttpStatusCode.NotFound,
			Error.Error => HttpStatusCode.InternalServerError,
			Error.TryAgain => HttpStatusCode.RequestTimeout,
			Error.Unauthorized => HttpStatusCode.Unauthorized,
			_ => HttpStatusCode.InternalServerError
		};
	}

	private class PostUserData {
		public string LoginName { get; set; }
		public string FullName { get; set; }
		public string[] Groups { get; set; }
		public string Password { get; set; }
	}

	private class PutUserData {
		public string FullName { get; set; }
		public string[] Groups { get; set; }
	}

	private class ResetPasswordData {
		public string NewPassword { get; set; }
	}

	private class ChangePasswordData {
		public string CurrentPassword { get; set; }
		public string NewPassword { get; set; }
	}
}
