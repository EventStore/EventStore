// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.Extensions.Caching.Memory;

namespace EventStore.Client;

public static class EventStoreClientSettingsExtensions {
	public static EventStoreClientSettings UseEventStoreCloud(this EventStoreClientSettings settings,
		string clientId, HttpClient identityProvider) {
		const int fiveMinutes = 60 * 5;
		var cache = new MemoryCache(new MemoryCacheOptions());
		var discoveryDocumentLazy = new Lazy<Task<DiscoveryDocumentResponse>>(
			() => identityProvider.GetDiscoveryDocumentAsync());

		settings.OperationOptions.GetAuthenticationHeaderValue = async (userCredentials, token) => {
			var authorizationHeader = userCredentials.ToString();
			return authorizationHeader[..7] == "Bearer "
				? $"Bearer {await GetAccessToken(authorizationHeader[7..])}"
				: null;
		};
		return settings;

		Task<string> GetAccessToken(string cloudToken) => cache.GetOrCreateAsync(cloudToken, async entry => {
			var discoveryDocument = await discoveryDocumentLazy.Value;

			var response = await identityProvider.RequestRefreshTokenAsync(new RefreshTokenRequest {
				RefreshToken = cloudToken,
				ClientId = clientId,
				Address = discoveryDocument.TokenEndpoint
			});

			if (response.Exception != null) {
				throw new Exception(response.Exception.Message, response.Exception);
			}

			if (response.IsError) {
				throw new InvalidOperationException(response.ErrorDescription);
			}

			entry.AbsoluteExpirationRelativeToNow = GetExpiration(response.ExpiresIn);

			return response.AccessToken;
		});

		static TimeSpan GetExpiration(int expiresIn) =>
			TimeSpan.FromSeconds(Math.Max(expiresIn - fiveMinutes, 0));
	}
}
