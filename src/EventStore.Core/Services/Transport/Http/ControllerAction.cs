// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Common.Utils;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;

namespace EventStore.Core.Services.Transport.Http;

public class ControllerAction(
	string uriTemplate,
	string httpMethod,
	ICodec[] requestCodecs,
	ICodec[] responseCodecs,
	Func<UriTemplateMatch, Operation> operation) {
	public readonly string UriTemplate = Ensure.NotNull(uriTemplate);
	public readonly string HttpMethod = Ensure.NotNull(httpMethod);
	public readonly Func<UriTemplateMatch, Operation> Operation = operation;
	public readonly ICodec[] SupportedRequestCodecs = Ensure.NotNull(requestCodecs);
	public readonly ICodec[] SupportedResponseCodecs = Ensure.NotNull(responseCodecs);
	public readonly ICodec DefaultResponseCodec = responseCodecs.Length > 0 ? responseCodecs[0] : null;

	public ControllerAction(string uriTemplate, string httpMethod, ICodec[] requestCodecs, ICodec[] responseCodecs, Operation operation)
		: this(uriTemplate, httpMethod, requestCodecs, responseCodecs, _ => operation) {
	}

	public bool Equals(ControllerAction other) {
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return Equals(other.UriTemplate, UriTemplate) && Equals(other.HttpMethod, HttpMethod);
	}

	public override bool Equals(object obj) {
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != typeof(ControllerAction)) return false;
		return Equals((ControllerAction)obj);
	}

	public override int GetHashCode() {
		unchecked {
			return (UriTemplate.GetHashCode() * 397) ^ HttpMethod.GetHashCode();
		}
	}

	public override string ToString() {
		return string.Format("UriTemplate: {0}, HttpMethod: {1}, SupportedCodecs: {2}, DefaultCodec: {3}",
			UriTemplate,
			HttpMethod,
			SupportedResponseCodecs,
			DefaultResponseCodec);
	}
}
