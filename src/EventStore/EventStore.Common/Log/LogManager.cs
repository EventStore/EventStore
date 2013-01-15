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
using System.Text;
using EventStore.Common.Utils;
using NLog;
using NLog.LayoutRenderers;

namespace EventStore.Common.Log
{
    [LayoutRenderer("logsdir")]
    public class NLogDirectoryLayoutRendered : LayoutRenderer
    {
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(LogManager._logsDirectory);
        }
    }

    public static class LogManager
    {
        static LogManager()
        {
            NLog.Config.ConfigurationItemFactory.Default.LayoutRenderers.RegisterDefinition("logsdir", typeof(NLogDirectoryLayoutRendered));
        }

        private static readonly ILogger GlobalLogger = GetLogger("GLOBAL-LOGGER");
        private static bool _initialized;
        internal static string _logsDirectory;

        public static string LogsDirectory
        {
            get
            {
                if (!_initialized)
                    throw new InvalidOperationException("Init method must be called");
                return _logsDirectory;
            }
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
            return new LazyLogger(() => new NLogger(logName));
        }

        public static void Init(string componentName, string logsDirectory)
        {
            Ensure.NotNull(componentName, "componentName");
            if (_initialized)
                throw new InvalidOperationException("Cannot initialize twice");

            _initialized = true;

            SetLogsDirectoryInternal(logsDirectory);
            SetComponentName(componentName);
            RegisterGlobalExceptionHandler();
        }

        private static void SetLogsDirectoryInternal(string logsDirectory)
        {
            _logsDirectory = logsDirectory;
        }

        private static void SetComponentName(string componentName)
        {
            Environment.SetEnvironmentVariable("EVENTSTORE_INT-COMPONENT-NAME", componentName, EnvironmentVariableTarget.Process);
        }

        private static void RegisterGlobalExceptionHandler()
        {
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
            NLogger.FlushLog();
            NLog.LogManager.Configuration = null;
        }
    }
}
