using System;

namespace EventStore.Core.Util; 

public class ExponentialBackoffRetryStrategy : RetryStrategy {
	private readonly TimeSpan _baseDelay;
	private TimeSpan _delayBetweenRetries;
	private DateTime _nextRetryTime;

	public ExponentialBackoffRetryStrategy(TimeSpan baseDelay) {
		_baseDelay = baseDelay;
		_delayBetweenRetries = baseDelay;
		_nextRetryTime = DateTime.UnixEpoch;
	}

	protected override void Reset() {
		_nextRetryTime = DateTime.UnixEpoch;
		_delayBetweenRetries = _baseDelay;
	}
	
	protected override void FailureAction() {
		_nextRetryTime = DateTime.Now + _delayBetweenRetries;
		_delayBetweenRetries += _delayBetweenRetries;
	}

	public override bool AreRetriesRemaining() {
		return true;
	}

	protected override bool IsReadyAction() {
		return DateTime.Now >= _nextRetryTime;
	}
}
