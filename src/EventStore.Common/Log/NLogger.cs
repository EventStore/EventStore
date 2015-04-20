using System;
using System.Linq;
using System.Threading;
using EventStore.Common.Utils;
using NLog;
using NLog.Conditions;

namespace EventStore.Common.Log
{
    public static class NLoggerHelperMethods
    {
        [ConditionMethod("is-dot-net")]
        public static bool IsDotNet()
        {
            return !Runtime.IsMono;
        }

        [ConditionMethod("is-mono")]
        public static bool IsMono()
        {
            return Runtime.IsMono;
        }
    }

    public class NLogger : ILogger
    {
        private readonly Logger _logger;

        public NLogger(string name)
        {
            _logger = NLog.LogManager.GetLogger(name);
        }

        public void Flush(TimeSpan? maxTimeToWait = null)
        {
            FlushLog(maxTimeToWait);
        }

        public void Fatal(string format, params object[] args)
        {
            _logger.Fatal(format, args);
        }

        public void Error(string format, params object[] args)
        {
            _logger.Error(format, args);
        }

        public void Info(string format, params object[] args)
        {
            _logger.Info(format, args);
        }

        public void Debug(string format, params object[] args)
        {
            _logger.Debug(format, args);
        }

        public void Trace(string format, params object[] args)
        {
            _logger.Trace(format, args);
        }

        public void FatalException(Exception exc, string format, params object[] args)
        {
            _logger.Debug(exc.ToString());
            _logger.FatalException(string.Format(format, args), exc);
        }

        public void ErrorException(Exception exc, string format, params object[] args)
        {
            _logger.ErrorException(string.Format(format, args), exc);
        }

        public void InfoException(Exception exc, string format, params object[] args)
        {
            _logger.InfoException(string.Format(format, args), exc);
        }

        public void DebugException(Exception exc, string format, params object[] args)
        {
            _logger.DebugException(string.Format(format, args), exc);
        }

        public void TraceException(Exception exc, string format, params object[] args)
        {
            _logger.TraceException(string.Format(format, args), exc);
        }

        public static void FlushLog(TimeSpan? maxTimeToWait = null)
        {
            var config = NLog.LogManager.Configuration;
            if (config == null)
                return;
            var asyncs = config.AllTargets.OfType<NLog.Targets.Wrappers.AsyncTargetWrapper>().ToArray();
            var countdown = new CountdownEvent(asyncs.Length);
            foreach (var wrapper in asyncs)
            {
                wrapper.Flush(x => countdown.Signal());
            }
            countdown.Wait(maxTimeToWait ?? TimeSpan.FromMilliseconds(500));
        }
    }
}
