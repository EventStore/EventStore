using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager {
	public interface IDelaySend {
		IDisposable DelaySend(TimeSpan delay, Message message);		
	}
}
