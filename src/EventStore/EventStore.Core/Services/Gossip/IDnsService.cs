using System;
using System.Net;

namespace EventStore.Core.Services.Gossip
{
    public interface IDnsService
    {
        IAsyncResult BeginGetHostAddresses(string hostNameOrAddress, AsyncCallback requestCallback, object state);
        IPAddress[] EndGetHostAddresses(IAsyncResult asyncResult);
    }
}