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
using System.Diagnostics;
using System.IO;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Util
{
    public class MiniWeb
    {
        private readonly string _localWebRootPath;
        private readonly string _fileSystemRoot;
        private readonly ILogger _logger = LogManager.GetLoggerFor<MiniWeb>();

        public MiniWeb(string localWebRootPath, string fileSystemRoot)
        {
            _logger.Info("Starting MiniWeb for {0} ==> {1}", localWebRootPath, fileSystemRoot);
            _localWebRootPath = localWebRootPath;
            _fileSystemRoot = fileSystemRoot;
        }

        public void RegisterControllerActions(IHttpService service)
        {
            var pattern = _localWebRootPath + "/{*remaining_path}";
            _logger.Trace("Binding MiniWeb to {0}", pattern);
            service.RegisterAction(new ControllerAction(pattern,
                                                                  HttpMethod.Get,
                                                                  Codec.NoCodecs,
                                                                  new ICodec[] { Codec.ManualEncoding }),
                                             OnStaticContent);
        }

        private void OnStaticContent(HttpEntityManager http, UriTemplateMatch match)
        {
            var contentLocalPath = match.BoundVariables["remaining_path"];
            ReplyWithContent(http, contentLocalPath);
        }

        private void ReplyWithContent(HttpEntityManager http, string contentLocalPath)
        {
            //NOTE: this is fix for Mono incompatibility in UriTemplate behavior for /a/b{*C}
            if (("/" + contentLocalPath).StartsWith(_localWebRootPath))
                contentLocalPath = contentLocalPath.Substring(_localWebRootPath.Length);

            //_logger.Trace("{0} requested from MiniWeb", contentLocalPath);
            try
            {
                var extensionToContentType = new Dictionary<string, string>
                {
                    { ".png",  "image/png"} ,
                    { ".jpg",  "image/jpeg"} ,
                    { ".jpeg", "image/jpeg"} ,
                    { ".css",  "text/css"} ,
                    { ".htm",  "text/html"} ,
                    { ".html", "text/html"} ,
                    { ".js",   "application/javascript"} ,
                    { ".json",   "application/json"} ,
                    { ".ico",  "image/vnd.microsoft.icon"}
                };

                var extension = Path.GetExtension(contentLocalPath);
                var fullPath = Path.Combine(_fileSystemRoot, contentLocalPath);

                string contentType;
                if (string.IsNullOrEmpty(extension)
                    || !extensionToContentType.TryGetValue(extension.ToLower(), out contentType)
                    || !File.Exists(fullPath))
                {
                    _logger.Info("Replying 404 for {0} ==> {1}", contentLocalPath, fullPath);
                    http.ReplyTextContent(
                        "Not Found", 404, "Not Found", "text/plain", null, 
                        ex => _logger.InfoException(ex, "Error while replying from MiniWeb"));
                }
                else
                {
                    var config = GetWebPageConfig(contentType);
                    var content = File.ReadAllBytes(fullPath);

                    http.Reply(content,
                                       config.Code,
                                       config.Description,
                                       config.ContentType,
                                       config.Encoding,
                                       config.Headers,
                                       ex => _logger.InfoException(ex, "Error while replying from MiniWeb"));
                }
            }
            catch (Exception ex)
            {
                http.ReplyTextContent(ex.ToString(), 500, "Internal Server Error", "text/plain", null, Console.WriteLine);
            }
        }

        private static ResponseConfiguration GetWebPageConfig(string contentType)
        {
            var encoding = contentType.StartsWith("image") ? null : Helper.UTF8NoBom;
            int? cacheSeconds =
#if RELEASE || CACHE_WEB_CONTENT
                60*60; // 1 hour
#else
                null; // no caching
#endif
// ReSharper disable ExpressionIsAlwaysNull
            return Configure.Ok(contentType, encoding, null, cacheSeconds, isCachePublic: true);
// ReSharper restore ExpressionIsAlwaysNull
        }

        public static string GetWebRootFileSystemDirectory(string debugPath = null)
        {
            string fileSystemWebRoot;
            try
            {
                if (!string.IsNullOrEmpty(debugPath))
                {
                    var sf = new StackFrame(0, true);
                    var fileName = sf.GetFileName();
                    var sourceWebRootDirectory = string.IsNullOrEmpty(fileName)
                                                     ? ""
                                                     : Path.GetFullPath(Path.Combine(fileName, @"..\..\..", debugPath));
                    fileSystemWebRoot = Directory.Exists(sourceWebRootDirectory)
                                            ? sourceWebRootDirectory
                                            : AppDomain.CurrentDomain.BaseDirectory;
                }
                else
                {
                    fileSystemWebRoot = AppDomain.CurrentDomain.BaseDirectory;
                }
            }
            catch (Exception)
            {
                fileSystemWebRoot = AppDomain.CurrentDomain.BaseDirectory;
            }
            return fileSystemWebRoot;
        }
    }
}