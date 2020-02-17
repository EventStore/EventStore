using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Transport.Http.EntityManagement;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public interface IHttpAuthenticationProvider {
		bool Authenticate(HttpContext context, out HttpAuthenticationRequest request);
	}
}
