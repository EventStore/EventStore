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
using System.IO;
using System.Net;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.Core.Services
{
    public class HttpSendService : IHandle<HttpMessage.HttpSend>,
                                   IHandle<HttpMessage.HttpBeginSend>,
                                   IHandle<HttpMessage.HttpSendPart>,
                                   IHandle<HttpMessage.HttpEndSend>,
                                   IHandle<HttpMessage.HttpForwardMessage>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<HttpSendService>();

        public void Handle(HttpMessage.HttpSend message)
        {
            var deniedToHandle = message.Message as HttpMessage.DeniedToHandle;
            if (deniedToHandle != null)
            {
                int code;
                switch (deniedToHandle.Reason)
                {
                    case DenialReason.ServerTooBusy:
                        code = HttpStatusCode.InternalServerError;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                message.HttpEntityManager.ReplyStatus(
                    code,
                    deniedToHandle.Details,
                    exc => Log.ErrorException(exc, "Error occurred while replying to HTTP with message {0}", message.Message));
            }
            else
            {
                var response = message.Data;
                var config = message.Configuration;
                message.HttpEntityManager.ReplyTextContent(
                    response,
                    config.Code,
                    config.Description,
                    config.ContentType,
                    config.Headers,
                    exc => Log.ErrorException(exc, "Error occurred while replying to HTTP with message {0}", message.Message));
            }
        }

        public void Handle(HttpMessage.HttpBeginSend message)
        {
            var config = message.Configuration;

            message.HttpEntityManager.BeginReply(config.Code, config.Description, config.ContentType, config.Encoding, config.Headers);
            if (message.Envelope != null)
                message.Envelope.ReplyWith(new HttpMessage.HttpCompleted(message.CorrelationId, message.HttpEntityManager));
        }

        public void Handle(HttpMessage.HttpSendPart message)
        {
            var response = message.Data;
            message.HttpEntityManager.ContinueReplyTextContent(
                response,
                exc => Log.ErrorException(exc, "Error occurred while replying to HTTP with message {0}", message),
                () =>
                {
                    if (message.Envelope != null)
                        message.Envelope.ReplyWith(new HttpMessage.HttpCompleted(message.CorrelationId, message.HttpEntityManager));
                });
        }

        public void Handle(HttpMessage.HttpEndSend message)
        {
            message.HttpEntityManager.EndReply();
            if (message.Envelope != null)
                message.Envelope.ReplyWith(new HttpMessage.HttpCompleted(message.CorrelationId, message.HttpEntityManager));
        }

        public void Handle(HttpMessage.HttpForwardMessage message)
        {
            var srcUrl = message.Manager.RequestedUrl;
            var srcBase = new Uri(string.Format("{0}://{1}:{2}/", srcUrl.Scheme, srcUrl.Host, srcUrl.Port));
            var forwardUri = new Uri(message.BaseUri, srcBase.MakeRelativeUri(srcUrl));
            ForwardRequest(message.Manager, forwardUri);
        }

        private static void ForwardRequest(HttpEntityManager manager, Uri forwardUri)
        {
            var srcReq = manager.HttpEntity.Request;
            var fwReq = (HttpWebRequest)WebRequest.Create(forwardUri);

            fwReq.Method = srcReq.HttpMethod;

            // Copy unrestricted headers (including cookies, if any)
            foreach (var headerKey in srcReq.Headers.AllKeys)
            {
                switch (headerKey)
                {
                    case "Accept":            fwReq.Accept = srcReq.Headers[headerKey]; break;
                    case "Connection":        break;
                    case "Content-Type":      fwReq.ContentType = srcReq.ContentType; break;
                    case "Content-Length":    fwReq.ContentLength = srcReq.ContentLength64; break;
                    case "Date":              fwReq.Date = DateTime.Parse(srcReq.Headers[headerKey]); break;
                    case "Expect":            break;
                    case "Host":              fwReq.Host = srcReq.Headers[headerKey]; break;
                    case "If-Modified-Since": fwReq.IfModifiedSince = DateTime.Parse(srcReq.Headers[headerKey]); break;
                    case "Proxy-Connection":  break;
                    case "Range":             break;
                    case "Referer":           fwReq.Referer = srcReq.Headers[headerKey]; break;
                    case "Transfer-Encoding": fwReq.TransferEncoding = srcReq.Headers[headerKey]; break;
                    case "User-Agent":        fwReq.UserAgent = srcReq.Headers[headerKey]; break;

                    default:
                        fwReq.Headers[headerKey] = srcReq.Headers[headerKey];
                        break;
                }
            }

            // Copy content (if content body is allowed)
            if (!string.Equals(srcReq.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(srcReq.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase)
                && srcReq.ContentLength64 > 0)
            {
                Task.Factory.FromAsync<Stream>(fwReq.BeginGetRequestStream, fwReq.EndGetRequestStream, null)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Log.ErrorException(t.Exception.InnerException,
                                               "Error on GetRequestStream for forwarded request for '{0}'.", manager.RequestedUrl);
                            ForwardReplyFailed(manager);
                            return;
                        }

                        new AsyncStreamCopier<object>(
                            srcReq.InputStream,
                            t.Result,
                            t.Result,
                            copier =>
                            {
                                var fwReqStream = (Stream)copier.AsyncState;
                                if (copier.Error != null)
                                {
                                    Log.ErrorException(copier.Error, "Error while forwarding request body from '{0}' to '{1}' ({2}).",
                                                       srcReq.Url, forwardUri, srcReq.HttpMethod);
                                    ForwardReplyFailed(manager);
                                }
                                else
                                {
                                    ForwardResponse(manager, fwReq);
                                }
                                Helper.EatException(fwReqStream.Close);
                            }).Start();
                    });
            }
            else
            {
                ForwardResponse(manager, fwReq);
            }
        }

        private static void ForwardReplyFailed(HttpEntityManager manager)
        {
            manager.ReplyStatus(HttpStatusCode.InternalServerError, "Error while forwarding request", _ => { });
        }

        private static void ForwardResponse(HttpEntityManager manager, HttpWebRequest fwReq)
        {
            Task.Factory.FromAsync<WebResponse>(fwReq.BeginGetResponse, fwReq.EndGetResponse, null)
                .ContinueWith(t =>
                {
                    HttpWebResponse response;
                    if (t.IsFaulted)
                    {
                        var exc = t.Exception.InnerException as WebException;
                        if (exc != null)
                        {
                            response = (HttpWebResponse)exc.Response;
                        }
                        else
                        {
                            Log.ErrorException(t.Exception.InnerException,
                                               "Error on EndGetResponse for forwarded request for '{0}'.", manager.RequestedUrl);
                            ForwardReplyFailed(manager);
                            return;
                        }
                    }
                    else
                    {
                        response = (HttpWebResponse) t.Result;
                    }

                    manager.ForwardReply(response, exc => Log.ErrorException(exc, "Error forwarding response for '{0}'.", manager.RequestedUrl));
                });
        }
    }
}
