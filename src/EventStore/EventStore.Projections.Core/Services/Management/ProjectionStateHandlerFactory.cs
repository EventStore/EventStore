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
using EventStore.Projections.Core.Services.v8;
using System.Linq;

namespace EventStore.Projections.Core.Services.Management
{
    public class ProjectionStateHandlerFactory
    {
        public IProjectionStateHandler Create(
            string factoryType, string source, Action<int, Action> cancelCallbackFactory = null,
            Action<string> logger = null)
        {
            var colonPos = factoryType.IndexOf(':');
            string kind = null;
            string rest = null;
            if (colonPos > 0)
            {
                kind = factoryType.Substring(0, colonPos);
                rest = factoryType.Substring(colonPos + 1);
            }
            else
            {
                kind = factoryType;
            }

            IProjectionStateHandler result;
            switch (kind.ToLowerInvariant())
            {
                case "js":
                    result = new DefaultV8ProjectionStateHandler(source, logger, cancelCallbackFactory);
                    break;
                case "native":
                    var type = Type.GetType(rest);
                    if (type == null)
                    {
                        //TODO: explicitly list all the assemblies to look for handlers
                        type =
                            AppDomain.CurrentDomain.GetAssemblies()
                                     .Select(v => v.GetType(rest))
                                     .FirstOrDefault(v => v != null);
                    }
                    var handler = Activator.CreateInstance(type, source, logger);
                    result = (IProjectionStateHandler)handler;
                    break;
                default:
                    throw new NotSupportedException(string.Format("'{0}' handler type is not supported", factoryType));
            }
            return result;
        }

    }
}