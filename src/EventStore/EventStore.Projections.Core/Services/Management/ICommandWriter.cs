using System;

namespace EventStore.Projections.Core.Services.Management
{
    public interface ICommandWriter
    {
        void PublishCommand(string command, Guid workerId, object body);
    }
}