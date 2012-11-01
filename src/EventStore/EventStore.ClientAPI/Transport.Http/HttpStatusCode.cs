// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
namespace EventStore.ClientAPI.Transport.Http
{
    internal static class HttpStatusCode
    {
        public const int Continue = (int) global::System.Net.HttpStatusCode.Continue;
        public const int SwitchingProtocols = (int) global::System.Net.HttpStatusCode.SwitchingProtocols;
        public const int OK = (int) global::System.Net.HttpStatusCode.OK;
        public const int Created = (int) global::System.Net.HttpStatusCode.Created;
        public const int Accepted = (int) global::System.Net.HttpStatusCode.Accepted;
        public const int NonAuthoritativeInformation = (int) global::System.Net.HttpStatusCode.NonAuthoritativeInformation;
        public const int NoContent = (int) global::System.Net.HttpStatusCode.NoContent;
        public const int ResetContent = (int) global::System.Net.HttpStatusCode.ResetContent;
        public const int PartialContent = (int) global::System.Net.HttpStatusCode.PartialContent;
        public const int Ambiguous = (int) global::System.Net.HttpStatusCode.Ambiguous;
        public const int MultipleChoices = (int) global::System.Net.HttpStatusCode.MultipleChoices;
        public const int Moved = (int) global::System.Net.HttpStatusCode.Moved;
        public const int MovedPermanently = (int) global::System.Net.HttpStatusCode.MovedPermanently;
        public const int Found = (int) global::System.Net.HttpStatusCode.Found;
        public const int Redirect = (int) global::System.Net.HttpStatusCode.Redirect;
        public const int RedirectMethod = (int) global::System.Net.HttpStatusCode.RedirectMethod;
        public const int SeeOther = (int) global::System.Net.HttpStatusCode.SeeOther;
        public const int NotModified = (int) global::System.Net.HttpStatusCode.NotModified;
        public const int UseProxy = (int) global::System.Net.HttpStatusCode.UseProxy;
        public const int Unused = (int) global::System.Net.HttpStatusCode.Unused;
        public const int RedirectKeepVerb = (int) global::System.Net.HttpStatusCode.RedirectKeepVerb;
        public const int TemporaryRedirect = (int) global::System.Net.HttpStatusCode.TemporaryRedirect;
        public const int BadRequest = (int) global::System.Net.HttpStatusCode.BadRequest;
        public const int Unauthorized = (int) global::System.Net.HttpStatusCode.Unauthorized;
        public const int PaymentRequired = (int) global::System.Net.HttpStatusCode.PaymentRequired;
        public const int Forbidden = (int) global::System.Net.HttpStatusCode.Forbidden;
        public const int NotFound = (int) global::System.Net.HttpStatusCode.NotFound;
        public const int MethodNotAllowed = (int) global::System.Net.HttpStatusCode.MethodNotAllowed;
        public const int NotAcceptable = (int) global::System.Net.HttpStatusCode.NotAcceptable;
        public const int ProxyAuthenticationRequired = (int) global::System.Net.HttpStatusCode.ProxyAuthenticationRequired;
        public const int RequestTimeout = (int) global::System.Net.HttpStatusCode.RequestTimeout;
        public const int Conflict = (int) global::System.Net.HttpStatusCode.Conflict;
        public const int Gone = (int) global::System.Net.HttpStatusCode.Gone;
        public const int LengthRequired = (int) global::System.Net.HttpStatusCode.LengthRequired;
        public const int PreconditionFailed = (int) global::System.Net.HttpStatusCode.PreconditionFailed;
        public const int RequestEntityTooLarge = (int) global::System.Net.HttpStatusCode.RequestEntityTooLarge;
        public const int RequestUriTooLong = (int) global::System.Net.HttpStatusCode.RequestUriTooLong;
        public const int UnsupportedMediaType = (int) global::System.Net.HttpStatusCode.UnsupportedMediaType;
        public const int RequestedRangeNotSatisfiable = (int) global::System.Net.HttpStatusCode.RequestedRangeNotSatisfiable;
        public const int ExpectationFailed = (int) global::System.Net.HttpStatusCode.ExpectationFailed;
        public const int InternalServerError = (int) global::System.Net.HttpStatusCode.InternalServerError;
        public const int NotImplemented = (int) global::System.Net.HttpStatusCode.NotImplemented;
        public const int BadGateway = (int) global::System.Net.HttpStatusCode.BadGateway;
        public const int ServiceUnavailable = (int) global::System.Net.HttpStatusCode.ServiceUnavailable;
        public const int GatewayTimeout = (int) global::System.Net.HttpStatusCode.GatewayTimeout;
        public const int HttpVersionNotSupported = (int) global::System.Net.HttpStatusCode.HttpVersionNotSupported;
    }
}