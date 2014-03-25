using System;

namespace EventStore.Common.Utils
{
    public static class Runtime
    {
        public static readonly bool IsMono = Type.GetType("Mono.Runtime") != null;
    }
}