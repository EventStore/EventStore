using System;
using EventStore.Common.Utils;

namespace EventStore.Common.Log
{
    public class LazyLogger : ILogger
    {
        private readonly Lazy<ILogger> _logger;

        public LazyLogger(Func<ILogger> factory)
        {
            Ensure.NotNull(factory, "factory");
            _logger = new Lazy<ILogger>(factory);
        }

        public void Flush(TimeSpan? maxTimeToWait = null)
        {
            _logger.Value.Flush(maxTimeToWait);
        }

        public void Fatal(string text)
        {
            _logger.Value.Fatal(text);
        }

        public void Error(string text)
        {
            _logger.Value.Error(text);
        }

        public void Info(string text)
        {
            _logger.Value.Info(text);
        }

        public void Debug(string text)
        {
            _logger.Value.Debug(text);
        }

        public void Trace(string text)
        {
            _logger.Value.Trace(text);
        }

        public void Fatal(string format, params object[] args)
        {
            _logger.Value.Fatal(format, args);
        }

        public void Error(string format, params object[] args)
        {
            _logger.Value.Error(format, args);
        }

        public void Info(string format, params object[] args)
        {
            _logger.Value.Info(format, args);
        }

        public void Debug(string format, params object[] args)
        {
            _logger.Value.Debug(format, args);
        }

        public void Trace(string format, params object[] args)
        {
            _logger.Value.Trace(format, args);
        }

        public void FatalException(Exception exc, string format)
        {
            _logger.Value.FatalException(exc, format);
        }

        public void ErrorException(Exception exc, string format)
        {
            _logger.Value.ErrorException(exc, format);
        }

        public void InfoException(Exception exc, string format)
        {
            _logger.Value.InfoException(exc, format);
        }

        public void DebugException(Exception exc, string format)
        {
            _logger.Value.DebugException(exc, format);
        }

        public void TraceException(Exception exc, string format)
        {
            _logger.Value.TraceException(exc, format);
        }

        public void FatalException(Exception exc, string format, params object[] args)
        {
            _logger.Value.FatalException(exc, format, args);
        }

        public void ErrorException(Exception exc, string format, params object[] args)
        {
            _logger.Value.ErrorException(exc, format, args);
        }

        public void InfoException(Exception exc, string format, params object[] args)
        {
            _logger.Value.InfoException(exc, format, args);
        }

        public void DebugException(Exception exc, string format, params object[] args)
        {
            _logger.Value.DebugException(exc, format, args);
        }

        public void TraceException(Exception exc, string format, params object[] args)
        {
            _logger.Value.TraceException(exc, format, args);
        }
    }
}
