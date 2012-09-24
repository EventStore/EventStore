using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Core.Services
{
    public static class SystemStreams
    {
        public const string StreamsStream = "$streams";
    }

    public static class SystemEventTypes
    {
        public const string StreamCreated = "$stream-created";
        public const string StreamDeleted = "$stream-deleted";
        public const string LinkTo = "$>";
    }
}
