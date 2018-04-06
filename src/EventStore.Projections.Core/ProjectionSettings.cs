using System;
using System.Collections.Generic;
using EventStore.Common.Options;
using EventStore.Core.Bus;

namespace EventStore.Projections.Core
{
    public class ProjectionSettings
    {
        private readonly GeneralProjectionSettings _generalProjectionSettings;
        private readonly V8ProjectionSettings _v8ProjectionSettings;
        public GeneralProjectionSettings General {get { return _generalProjectionSettings; }}
        public V8ProjectionSettings V8 {get { return _v8ProjectionSettings; }}

        public ProjectionSettings(
            GeneralProjectionSettings generalProjectionSettings,
            V8ProjectionSettings v8ProjectionSettings)
        {
            _generalProjectionSettings = generalProjectionSettings;
            _v8ProjectionSettings = v8ProjectionSettings;
        }
    }
    public class V8ProjectionSettings
    {
        private readonly TimeSpan _compileTimeout;
        private readonly TimeSpan _eventProcessTimeout;
        public TimeSpan CompileTimeout {get { return _compileTimeout; }}
        public TimeSpan EventProcessTimeout {get { return _eventProcessTimeout; }}

        public V8ProjectionSettings(
            TimeSpan compileTimeout,
            TimeSpan eventProcessTimeout
        ){
            _compileTimeout = compileTimeout;
            _eventProcessTimeout = eventProcessTimeout;
        }
    }

    public class GeneralProjectionSettings
    {
        private readonly TimeSpan _projectionQueryExpiry;
        public TimeSpan ProjectionQueryExpiry {get { return _projectionQueryExpiry; }}

        public GeneralProjectionSettings(
            TimeSpan projectionQueryExpiry
        ){
            _projectionQueryExpiry = projectionQueryExpiry;
        }
    }
}