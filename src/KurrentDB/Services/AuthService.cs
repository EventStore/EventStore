// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Blazored.LocalStorage;

namespace KurrentDB.Services;

public class AuthService(JwtTokenService jwtTokenService, ILocalStorageService sessionService) {
	const string AuthTokenName = "auth_token";

	public event Action<ClaimsPrincipal> UserChanged;
	ClaimsPrincipal _currentUser;

	public ClaimsPrincipal CurrentUser {
		get => _currentUser ?? new();
		set {
			_currentUser = value;
			UserChanged?.Invoke(_currentUser);
		}
	}

	public bool IsLoggedIn => CurrentUser.Identity?.IsAuthenticated ?? false;

	public async Task LogoutAsync() {
		var authToken = await sessionService.GetItemAsStringAsync(AuthTokenName);
		if (!string.IsNullOrEmpty(authToken)) {
			await sessionService.RemoveItemAsync(AuthTokenName);
		}
		CurrentUser = new();
	}

	public async Task<bool> GetStateFromTokenAsync() {
		var authToken = await sessionService.GetItemAsStringAsync(AuthTokenName);
		var result = jwtTokenService.TryValidateToken(authToken, out var identity);

		if (!result) {
			await LogoutAsync();
			return false;
		}

		CurrentUser = new(identity);
		return true;
	}

	public async Task Login(ClaimsPrincipal user) {
		CurrentUser = user;
		var token = jwtTokenService.CreateToken(user);
		await sessionService.SetItemAsStringAsync(AuthTokenName, token);
	}
}
