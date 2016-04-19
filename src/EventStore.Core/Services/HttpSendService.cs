using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Histograms;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;

namespace EventStore.Core.Services
{
    public class HttpSendService : IHttpForwarder,
                                   IHandle<SystemMessage.StateChangeMessage>,
                                   IHandle<HttpMessage.SendOverHttp>,
                                   IHandle<HttpMessage.HttpSend>,
                                   IHandle<HttpMessage.HttpBeginSend>,
                                   IHandle<HttpMessage.HttpSendPart>,
                                   IHandle<HttpMessage.HttpEndSend>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<HttpSendService>();

        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private readonly HttpMessagePipe _httpPipe;
        private readonly bool _forwardRequests;
        private const string _httpSendHistogram = "http-send";
        private VNodeInfo _masterInfo;

        public HttpSendService(HttpMessagePipe httpPipe, bool forwardRequests)
        {
            Ensure.NotNull(httpPipe, "httpPipe");
            _httpPipe = httpPipe;
            _forwardRequests = forwardRequests;
        }

        public void Handle(SystemMessage.StateChangeMessage message)
        {
            switch (message.State)
            {
                case VNodeState.PreReplica:
                case VNodeState.CatchingUp:
                case VNodeState.Clone:
                case VNodeState.Slave:
                    _masterInfo = ((SystemMessage.ReplicaStateMessage) message).Master;
                    break;
                case VNodeState.Initializing:
                case VNodeState.Unknown:
                case VNodeState.PreMaster:
                case VNodeState.Master:
                case VNodeState.Manager:
                case VNodeState.ShuttingDown:
                case VNodeState.Shutdown:
                    _masterInfo = null;
                    break;
                default:
                    throw new Exception(string.Format("Unknown node state: {0}.", message.State));
            }
        }

        public void Handle(HttpMessage.SendOverHttp message)
        {
            if(message.LiveUntil > DateTime.Now) {
                _httpPipe.Push(message.Message, message.EndPoint);
            } else {
                Log.Debug("Dropping HTTP send message due to TTL being over. {1} To : {0}", message.EndPoint, message.Message.GetType().Name.ToString());
            }
        }

        public void Handle(HttpMessage.HttpSend message)
        {
            var deniedToHandle = message.Message as HttpMessage.DeniedToHandle;
            if (deniedToHandle != null)
            {
                int code;
                switch (deniedToHandle.Reason)
                {
                    case DenialReason.ServerTooBusy:
                        code = HttpStatusCode.ServiceUnavailable;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                var start = _watch.ElapsedTicks;
                message.HttpEntityManager.ReplyStatus(
                    code,
                    deniedToHandle.Details,
                    exc => Log.Debug("Error occurred while replying to HTTP with message {0}: {1}.", message.Message, exc.Message));
                HistogramService.SetValue(_httpSendHistogram,
                   (long)((((double)_watch.ElapsedTicks - start) / Stopwatch.Frequency) * 1000000000));
            }
            else
            {
                var response = message.Data;
                var config = message.Configuration;
                var start = _watch.ElapsedTicks;
                message.HttpEntityManager.ReplyTextContent(
                    response,
                    config.Code,
                    config.Description,
                    config.ContentType,
                    config.Headers,
                    exc => Log.Debug("Error occurred while replying to HTTP with message {0}: {1}.", message.Message, exc.Message));
                HistogramService.SetValue(_httpSendHistogram,
                   (long)((((double)_watch.ElapsedTicks - start) / Stopwatch.Frequency) * 1000000000));

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
                exc => Log.Debug("Error occurred while replying to HTTP with message {0}: {1}.", message, exc.Message),
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

        bool IHttpForwarder.ForwardRequest(HttpEntityManager manager)
        {
            var masterInfo = _masterInfo;
            if (_forwardRequests && masterInfo != null)
            {
                var srcUrl = manager.RequestedUrl;
                var srcBase = new Uri(string.Format("{0}://{1}:{2}/", srcUrl.Scheme, srcUrl.Host, srcUrl.Port), UriKind.Absolute);
                var baseUri = new Uri(string.Format("http://{0}/", masterInfo.InternalHttp));
                var forwardUri = new Uri(baseUri, srcBase.MakeRelativeUri(srcUrl));
                ForwardRequest(manager, forwardUri);
                return true;
            }
            return false;
        }

        private static void ForwardRequest(HttpEntityManager manager, Uri forwardUri)
        {
            var srcReq = manager.HttpEntity.Request;
            var fwReq = (HttpWebRequest)WebRequest.Create(forwardUri);

            fwReq.Method = srcReq.HttpMethod;
            // Copy unrestricted headers (including cookies, if any)
            foreach (var headerKey in srcReq.Headers.AllKeys)
            {
                switch (headerKey.ToLower())
                {
                    case "accept":            fwReq.Accept = srcReq.Headers[headerKey]; break;
                    case "connection":        break;
                    case "content-type":      fwReq.ContentType = srcReq.ContentType; break;
                    case "content-length":    fwReq.ContentLength = srcReq.ContentLength64; break;
                    case "date":              fwReq.Date = DateTime.Parse(srcReq.Headers[headerKey]); break;
                    case "expect":            break;
                    case "host":              fwReq.Headers["X-Forwarded-Host"] = srcReq.Headers[headerKey];
                                              fwReq.Host = forwardUri.Host; break;
                    case "if-modified-since": fwReq.IfModifiedSince = DateTime.Parse(srcReq.Headers[headerKey]); break;
                    case "proxy-connection":  break;
                    case "range":             break;
                    case "referer":           fwReq.Referer = srcReq.Headers[headerKey]; break;
                    case "transfer-encoding": fwReq.TransferEncoding = srcReq.Headers[headerKey]; break;
                    case "user-agent":        fwReq.UserAgent = srcReq.Headers[headerKey]; break;

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
                        if (t.Exception != null)
                        {
                            Log.Debug("Error on GetRequestStream for forwarded request for '{0}': {1}.",
                                      manager.RequestedUrl, t.Exception.InnerException.Message);
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
                                    Log.Debug("Error while forwarding request body from '{0}' to '{1}' ({2}): {3}.",
                                              srcReq.Url, forwardUri, srcReq.HttpMethod, copier.Error.Message);
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
                    if (t.Exception != null)
                    {
                        var exc = t.Exception.InnerException as WebException;
                        if (exc != null)
                        {
                            response = (HttpWebResponse)exc.Response;
                        }
                        else
                        {
                            Log.Debug("Error on EndGetResponse for forwarded request for '{0}': {1}.",
                                      manager.RequestedUrl, t.Exception.InnerException.Message);
                            ForwardReplyFailed(manager);
                            return;
                        }
                    }
                    else
                    {
                        response = (HttpWebResponse) t.Result;
                    }

                    manager.ForwardReply(response, exc => Log.Debug("Error forwarding response for '{0}': {1}.", manager.RequestedUrl, exc.Message));
                });
        }
    }
}
