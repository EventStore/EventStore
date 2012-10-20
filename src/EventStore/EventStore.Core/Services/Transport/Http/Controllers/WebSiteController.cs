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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Services;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class WebSiteController : CommunicationController
    {
        private readonly MiniWeb _miniWeb;

        public WebSiteController(IPublisher publisher)
            : base(publisher)
        {
            string fileSystemWebRoot = MiniWeb.GetWebRootFileSystemDirectory("EventStore.Web");
            _miniWeb = new MiniWeb("/web", Path.Combine(fileSystemWebRoot, @"web"));
        }

        protected override void SubscribeCore(IHttpService service, HttpMessagePipe pipe)
        {
            _miniWeb.RegisterControllerActions(service);
            RegisterRedirectAction(service, "", "/web/index.htm");
            RegisterRedirectAction(service, "/web/projections", "/web/list-projections.htm");
        }

        private static void RegisterRedirectAction(IHttpService service, string fromUrl, string toUrl)
        {
            service.RegisterControllerAction(
                new ControllerAction(
                    fromUrl, 
                    HttpMethod.Get, 
                    Codec.NoCodecs, 
                    new ICodec[] { Codec.ManualEncoding }, Codec.ManualEncoding),
                    (http, match) => http.Manager.Reply(
                        "Moved", 302, "Found", "text/plain",
                        new[]
                            {
                                new KeyValuePair<string, string>(
                                    "Location",   new Uri(match.BaseUri, toUrl).AbsoluteUri)
                            }, Console.WriteLine));
        }
    }
}
