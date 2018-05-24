using System;
using System.Linq;
using System.Threading;
using EventStore.Common.Utils;
using Serilog;
using Serilog.Core;

namespace EventStore.Common.Log
{
    public static class SeriLoggerHelperMethods
    {
        //[ConditionMethod("is-dot-net")]
        public static bool IsDotNet()
        {
            return !Runtime.IsMono;
        }

        //[ConditionMethod("is-mono")]
        public static bool IsMono()
        {
            return Runtime.IsMono;
        }
    }

    public class SeriLogger : ILogger
    {
        private readonly Serilog.ILogger _logger;
        //private string _name;
        //private readonly ILogger _slogger;

        public SeriLogger(string name)
        {
            //_logger = NLog.LogManager.GetLogger(name);
            //_logger = Serilog.Configuration.GetLogger(name);
           //_logger = Serilog.Log.ForContext(this.GetType());
           _logger = Serilog.Log.ForContext(name, null);

        }

        public void Flush(TimeSpan? maxTimeToWait = null)
        {
            FlushLog(maxTimeToWait);
        }

        public void Fatal(string format, params object[] args)
        {
            _logger.ForContext<SeriLogger>().Fatal(format, args);
        }

        public void Error(string format, params object[] args)
        {
            //Serilog.Log.Error(format, args);
            _logger.Error(format, args);
        }

        public void Info(string format, params object[] args)
        {
            _logger.Information(format, args);
        }

        public void Debug(string format, params object[] args)
        {
            _logger.Debug(format, args);
        }

        public void Warn(string format, params object[] args)
        {
            _logger.Warning(format, args);
        }

        public void Trace(string format, params object[] args)
        {
            _logger.Verbose(format, args);
        }

        public void FatalException(Exception exc, string format, params object[] args)
        {
            _logger.Fatal(exc, format, args);
        }

        public void ErrorException(Exception exc, string format, params object[] args)
        {
            _logger.Error(exc, format, args);
        }

        public void InfoException(Exception exc, string format, params object[] args)
        {
             _logger.Information(exc, format, args);
        }

        public void DebugException(Exception exc, string format, params object[] args)
        {
            _logger.Debug(exc, format, args);
        }

        public void TraceException(Exception exc, string format, params object[] args)
        {
            _logger.Verbose(exc, format, args);
        }

        public static void FlushLog(TimeSpan? maxTimeToWait = null)
        {
             Serilog.Log.CloseAndFlush();
        }
    }
}
