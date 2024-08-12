using System;

namespace EventStore.Core.Util; 

public abstract class RetryStrategy {

	public int FailureCnt { get; private set; } = 0;

	//to be called when task fails
	public bool Failed(Exception ex = null, Action<Exception> onRetriesExhausted = null) {
		FailureCnt++;
		FailureAction();
		if (!AreRetriesRemaining()) {
			onRetriesExhausted?.Invoke(ex);
			return true;
		}
		
		return false;
	}
	
	//to be called when task succeeds
	public void Success() {
		Reset();
	}
	
	public bool IsReady() {
		return AreRetriesRemaining() && IsReadyAction();
	}
	
	protected abstract void Reset();

	//update internal state of the strategy. it will called when a task fails
	protected abstract void FailureAction();

	public abstract bool AreRetriesRemaining();
	
	//returns true if the task is ready to be processed; otherwise returns false
	protected abstract bool IsReadyAction();
}
