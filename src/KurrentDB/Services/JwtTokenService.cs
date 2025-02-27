// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace KurrentDB.Services;

public class JwtTokenService {
	const int TokenHours = 1;

	readonly byte[] _token = Encoding.UTF8.GetBytes(GenerateRandomString(100));

	public string CreateToken(ClaimsPrincipal user) {
		var key = new SymmetricSecurityKey(_token);
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
		var token = new JwtSecurityToken(
			claims: user.Claims,
			expires: DateTime.Now.AddHours(TokenHours),
			signingCredentials: creds
		);
		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	public bool TryValidateToken(string token, [NotNullWhen(true)] out ClaimsIdentity identity) {
		if (string.IsNullOrEmpty(token)) {
			identity = new();
			return false;
		}

		try {
			var tokenHandler = new JwtSecurityTokenHandler();
			tokenHandler.ValidateToken(token, new() {
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(_token),
				ValidateIssuer = false,
				ValidateAudience = false,
				ClockSkew = TimeSpan.Zero
			}, out var validatedToken);
			var jwtToken = (JwtSecurityToken)validatedToken;
			identity = new(jwtToken.Claims, "jwt");
			return true;
		} catch (Exception) {
			identity = new();
			return false;
		}
	}

	public static string GenerateRandomString(int length) {
		var bytes = new byte[length];
		using (var rng = RandomNumberGenerator.Create()) {
			rng.GetBytes(bytes);
		}

		var chars = new char[length];
		for (int i = 0; i < length; i++) {
			chars[i] = GetRandomChar(bytes[i]);
		}

		return new string(chars);
	}

	static char GetRandomChar(byte b) {
		if (b < 26) return (char)('a' + b);
		if (b < 52) return (char)('A' + b - 26);
		if (b < 62) return (char)('0' + b - 52);
		if (b < 94) return (char)('!' + b - 62);
		return (char)(' ' + b - 94);
	}
}
