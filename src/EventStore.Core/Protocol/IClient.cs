using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Transport.Common;

namespace EventStore.Core.Protocol;

public interface IClient {
	Task<long> WriteAsync(string stream, EventToWrite[] events, long expectedVersion, CancellationToken cancellationToken);
	IAsyncEnumerable<Event> ReadStreamForwards(string stream, StreamRevision start, long maxCount, CancellationToken cancellationToken);
	IAsyncEnumerable<Event> ReadStreamBackwards(string stream, long maxCount, CancellationToken cancellationToken);
}
