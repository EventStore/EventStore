using System;
using System.Security.Principal;
using EventStore.Core.Authentication;

namespace EventStore.ClientAPI.Embedded
{
    internal class EmbeddedAuthenticationRequest : AuthenticationRequest
    {
        private readonly Action<IPrincipal> _onAuthenticated;
        private readonly EmbeddedResponseEnvelope _envelope;

        internal EmbeddedAuthenticationRequest(
            string name, string suppliedPassword, EmbeddedResponseEnvelope envelope, Action<IPrincipal> onAuthenticated)
            : base(name, suppliedPassword)
        {
            _onAuthenticated = onAuthenticated;
            _envelope = envelope;
        }

        public override void Unauthorized()
        {
            _envelope.NotAuthenticated();
        }

        public override void Authenticated(IPrincipal principal)
        {
            _onAuthenticated(principal);
        }

        public override void Error()
        {
            _envelope.ServerError();
        }

        public override void NotReady()
        {
            _envelope.NotReady();
        }
    }
}