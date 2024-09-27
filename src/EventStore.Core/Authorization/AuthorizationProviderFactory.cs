// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization {
	public class AuthorizationProviderFactory {
		private readonly Func<AuthorizationProviderFactoryComponents, IAuthorizationProviderFactory>
			_authorizationProviderFactory;

		public AuthorizationProviderFactory(
			Func<AuthorizationProviderFactoryComponents, IAuthorizationProviderFactory>
				authorizationProviderFactory) {
			_authorizationProviderFactory = authorizationProviderFactory;
		}

		public IAuthorizationProviderFactory GetFactory(
			AuthorizationProviderFactoryComponents authorizationProviderFactoryComponents) =>
			_authorizationProviderFactory(authorizationProviderFactoryComponents);
	}
}
