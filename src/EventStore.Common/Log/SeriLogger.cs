using System;
using Serilog.Events;

namespace EventStore.Common.Log
{
    /* 
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
     */

    public class SeriLogger : ILogger
    {
        private readonly Serilog.ILogger _logger;
        private string _name;
        //private readonly ILogger _slogger;
        public SeriLogger(string name)
        {
            //_logger = NLog.LogManager.GetLogger(name);
            //_logger = Serilog.Configuration.GetLogger(name);
           //_logger = Serilog.Log.ForContext(this.GetType());
           _logger = Serilog.Log.ForContext(name, null);
           _name = name;
        }

        public void Flush(TimeSpan? maxTimeToWait = null)
        {
            FlushLog(maxTimeToWait);
        }

        public void Fatal(string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","FATAL",false)
            .Fatal(format, args);
        }

        public void Error(string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","ERROR",false)
            .Error(format, args);
        }

        public void Info(string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","INFO",false)
            .Information(format, args);
        }

        public void Debug(string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","DEBUG",false)
            .Debug(format, args);
        }

        public void Warn(string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","WARN",false)
            .Warning(format, args);
        }

        public void Trace(string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","TRACE",false)
            .Verbose(format, args);
        }

        public void FatalException(Exception exc, string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","FATAL",false)
            .Fatal(exc, format, args);
        }

        public void ErrorException(Exception exc, string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","ERROR",false)
            .Error(exc, format, args);
        }

        public void InfoException(Exception exc, string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","INFO",false)
            .Information(exc, format, args);
        }

        public void DebugException(Exception exc, string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","DEBUG",false)
            .Debug(exc, format, args);
        }

        public void TraceException(Exception exc, string format, params object[] args)
        {
            _logger
            .ForContext("className",_name,false)
            .ForContext("loglevel","TRACE",false)
            .Verbose(exc, format, args);
        }

        public static void FlushLog(TimeSpan? maxTimeToWait = null)
        {
             Serilog.Log.CloseAndFlush();
        }
    }
}
