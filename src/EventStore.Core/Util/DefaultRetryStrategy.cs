using System;
using System.Data;

namespace EventStore.Core.Util;

public class DefaultRetryStrategy : RetryStrategy {
	private readonly TimeSpan _delayBetweenRetries;
	private int _retriesRemaining;
	private readonly int _totalRetries;
	private DateTime _nextRetryTime;
	public const int DefaultNumRetries = 5;

	public DefaultRetryStrategy(TimeSpan delayBetweenRetries, int totalRetries = DefaultNumRetries) {
		if (totalRetries < 0) {
			throw new ConstraintException($"Number of retries should be non-negative. Found : {totalRetries}");
		}

		_delayBetweenRetries = delayBetweenRetries;
		_totalRetries = totalRetries;
		_retriesRemaining = totalRetries;
		_nextRetryTime = DateTime.UnixEpoch;
	}

	protected override void Reset() {
		_retriesRemaining = _totalRetries;
		_nextRetryTime = DateTime.UnixEpoch;
	}
	
	protected override void FailureAction() {
		_retriesRemaining--;
		_nextRetryTime = DateTime.Now + _delayBetweenRetries;
	}

	public override bool AreRetriesRemaining() {
		return _retriesRemaining > 0;
	}

	protected override bool IsReadyAction() {
		return DateTime.Now >= _nextRetryTime;
	}
}
