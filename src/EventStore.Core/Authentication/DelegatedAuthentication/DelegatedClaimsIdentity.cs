using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace EventStore.Core.Authentication.DelegatedAuthentication;

public class DelegatedClaimsIdentity(IReadOnlyDictionary<string, string> tokens) :
	ClaimsIdentity(tokens.Select(pair => new Claim(pair.Key, pair.Value)));