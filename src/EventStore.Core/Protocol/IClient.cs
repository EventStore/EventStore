using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Protocol;

public interface IClient {
	Task<long> WriteAsync(string stream, EventToWrite[] events, long expectedVersion, CancellationToken cancellationToken);
	IAsyncEnumerable<Event> ReadStreamForwards(string stream, long maxCount, CancellationToken cancellationToken);
	IAsyncEnumerable<Event> ReadStreamBackwards(string stream, long maxCount, CancellationToken cancellationToken);
}
