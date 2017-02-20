using System;

namespace EventStore.Core.Helpers
{
    public static class ExpectedVersionConverter
    {
        public static int ConvertTo32Bit(long expectedVersion) {
            if(expectedVersion == long.MaxValue) {
                return int.MaxValue;
            }
            if(expectedVersion > int.MaxValue) {
                throw new Exception(string.Format("Expected version {0} of event is greater than int.MaxValue. Cannot downgrade", expectedVersion));
            }
            return (int)expectedVersion;
        }

        public static int? ConvertTo32Bit(long? expectedVersion) {
            if(expectedVersion == null) {
                return null;
            }
            if(expectedVersion == long.MaxValue) {
                return int.MaxValue;
            }
            if(expectedVersion > int.MaxValue) {
                throw new Exception(string.Format("Expected version {0} of event is greater than int.MaxValue. Cannot downgrade", expectedVersion));
            }
            return (int)expectedVersion;
        }
    }
}