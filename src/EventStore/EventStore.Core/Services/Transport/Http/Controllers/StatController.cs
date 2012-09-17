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
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class StatController : CommunicationController
    {
        private static readonly ICodec[] SupportedCodecs = new ICodec[] { Codec.Json, Codec.Xml };
        private static readonly ICodec DefaultResponseCodec = Codec.Json;

        public StatController(IPublisher publisher)
            : base(publisher)
        {
        }

        protected override void SubscribeCore(IHttpService service, HttpMessagePipe pipe)
        {
            Ensure.NotNull(service, "service");
            Ensure.NotNull(pipe, "pipe");

            service.RegisterControllerAction(new ControllerAction("/stats/{*statPath}",
                                                                  HttpMethod.Get,
                                                                  Codec.NoCodecs,
                                                                  SupportedCodecs,
                                                                  DefaultResponseCodec),
                                             OnGetFreshStats);
        }

        private void OnGetFreshStats(HttpEntity entity, UriTemplateMatch match)
        {
            var envelope = new SendToHttpEnvelope(
                    entity,
                    Format.GetFreshStatsCompleted,
                    Configure.GetFreshStatsCompleted);

            var statPath = match.BoundVariables["statPath"];
            var statSelector = GetStatSelector(statPath);

            Publish(new MonitoringMessage.GetFreshStats(envelope, statSelector));
        }

        private static Func<Dictionary<string, object>, Dictionary<string, object>> GetStatSelector(string statPath)
        {
            if (string.IsNullOrEmpty(statPath))
                return dict => dict;

            //NOTE: this is fix for Mono incompatibility in UriTemplate behavior for /a/b{*C}
            //todo: use IsMono here?
            if (statPath.StartsWith("stats/"))
            {
                statPath = statPath.Substring(6);
                if (string.IsNullOrEmpty(statPath))
                    return dict => dict;
            }

            var groups = statPath.Split('/');

            return dict =>
            {
                Ensure.NotNull(dict, "dictionary");

                foreach (string groupName in groups)
                {
                    object item;
                    if (!dict.TryGetValue(groupName, out item))
                        return null;

                    dict = item as Dictionary<string, object>;

                    if (dict == null)
                        return null;
                }

                return dict;
            };
        }
    }
}
