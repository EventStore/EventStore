using System;
using EventStore.Core.Helpers;

namespace EventStore.Projections.Core.Services.Management
{
    public interface ICommandWriter
    {
        void PublishCommand(string command, Guid workerId, object body);
    }

    public sealed class CommandWriter : ICommandWriter
    {
        private readonly IODispatcher _ioDispatcher;

        public CommandWriter(IODispatcher ioDispatcher)
        {
            _ioDispatcher = ioDispatcher;
        }

        public void PublishCommand(string command, Guid workerId, object body)
        {
            throw new System.NotImplementedException();
        }
    }
}