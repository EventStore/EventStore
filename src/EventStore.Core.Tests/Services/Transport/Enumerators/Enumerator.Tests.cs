using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Core.Services.Transport.Enumerators;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Enumerators;

[TestFixture]
public partial class EnumeratorTests {
	public record SubscriptionResponse { }
	public record Event(Guid Id) : SubscriptionResponse { }
	public record SubscriptionConfirmation() : SubscriptionResponse { }
	public record CaughtUp : SubscriptionResponse { }

	public class EnumeratorWrapper : IAsyncDisposable {
		private readonly IAsyncEnumerator<ReadResponse> _enumerator;

		public EnumeratorWrapper(IAsyncEnumerator<ReadResponse> enumerator) {
			_enumerator = enumerator;
		}

		public ValueTask DisposeAsync() => _enumerator.DisposeAsync();

		public async Task<SubscriptionResponse> GetNext() {
			if (!await _enumerator.MoveNextAsync()) {
				throw new Exception("No more items in enumerator");
			}

			var resp = _enumerator.Current;

			return resp switch {
				ReadResponse.EventReceived eventReceived => new Event(eventReceived.Event.Event.EventId),
				ReadResponse.SubscriptionConfirmed => new SubscriptionConfirmation(),
				ReadResponse.SubscriptionCaughtUp => new CaughtUp(),
				_ => throw new ArgumentOutOfRangeException(nameof(resp), resp, null),
			};
		}
	}
}
