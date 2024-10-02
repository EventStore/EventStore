// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace EventStore.Core.Authentication.DelegatedAuthentication;

public class DelegatedClaimsIdentity(IReadOnlyDictionary<string, string> tokens) :
	ClaimsIdentity(tokens.Select(pair => new Claim(pair.Key, pair.Value)));
