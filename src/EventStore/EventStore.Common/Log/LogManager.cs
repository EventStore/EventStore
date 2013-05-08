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
using EventStore.Common.Utils;

namespace EventStore.Common.Log
{
    public static class LogManager
    {
        public static string LogsDirectory
        {
            get
            {
                if (!_initialized)
                    throw new InvalidOperationException("Init method must be called");
                return _logsDirectory;
            }
        }

        private static readonly ILogger GlobalLogger = GetLogger("GLOBAL-LOGGER");
        private static bool _initialized;
        private static Func<string, ILogger> _logFactory = x => new NLogger(x);
        internal static string _logsDirectory;

        static LogManager()
        {
            var conf = NLog.Config.ConfigurationItemFactory.Default;
            conf.LayoutRenderers.RegisterDefinition("logsdir", typeof(NLogDirectoryLayoutRendered));
            conf.ConditionMethods.RegisterDefinition("is-dot-net", typeof(NLoggerHelperMethods).GetMethod("IsMono"));
            conf.ConditionMethods.RegisterDefinition("is-mono", typeof(NLoggerHelperMethods).GetMethod("IsDotNet"));
        }

        public static ILogger GetLoggerFor(Type type)
        {
            return GetLogger(type.Name);
        }

        public static ILogger GetLoggerFor<T>()
        {
            return GetLogger(typeof(T).Name);
        }

        public static ILogger GetLogger(string logName)
        {
            return new LazyLogger(() => _logFactory(logName));
        }

        public static void Init(string componentName, string logsDirectory)
        {
            Ensure.NotNull(componentName, "componentName");
            if (_initialized)
                throw new InvalidOperationException("Cannot initialize twice");

            _initialized = true;

            _logsDirectory = logsDirectory;
            Environment.SetEnvironmentVariable("EVENTSTORE_INT-COMPONENT-NAME", componentName, EnvironmentVariableTarget.Process);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var exc = e.ExceptionObject as Exception;
                if (exc != null)
                    GlobalLogger.FatalException(exc, "Global Unhandled Exception occurred.");
                else
                    GlobalLogger.Fatal("Global Unhandled Exception object: {0}.", e.ExceptionObject);
                GlobalLogger.Flush(TimeSpan.FromMilliseconds(500));
            };
        }

        public static void Finish()
        {
            try
            {
                GlobalLogger.Flush();
                NLog.LogManager.Configuration = null;
            }
            catch (Exception exc)
            {
                GlobalLogger.ErrorException(exc, "Exception during flushing logs, ignoring...");
            }
        }

        public static void SetLogFactory(Func<string, ILogger> factory)
        {
            Ensure.NotNull(factory, "factory");
            _logFactory = factory;
        }
    }
}
