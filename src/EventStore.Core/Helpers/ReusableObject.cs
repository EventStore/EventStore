using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace EventStore.Core.Helpers {
	// Thread-safe reusable object: similar concept as an ObjectPool<T> but has only one object and one user at a time.
	// If used correctly, there should not be any contention since there can be only one user of the object at a time.
	// However, some synchronization is done since the Acquire() and Release() methods are allowed to be called from
	// different threads.
	public static class ReusableObject {
		public static ReusableObject<T> Create<T>(T t) where T : IReusableObject =>
			new ReusableObject<T>(t);
	}

	public class ReusableObject<TObject> where TObject : IReusableObject{
		private readonly TObject _object;
		private int _state;

		private enum State {
			Free = 0,
			LockedToAcquire = 1,
			Acquired = 2
		}

		public ReusableObject(TObject obj) {
			_state = (int) State.Free;
			_object = obj;
		}

		public TObject Acquire(IReusableObjectInitParams initParams) {
			TrySwitchState(State.Free, State.LockedToAcquire);
			_object.Reset();
			_object.Initialize(initParams);
			TrySwitchState(State.LockedToAcquire, State.Acquired);
			return _object;
		}

		public void Release() {
			TrySwitchState(State.Acquired, State.Free);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TrySwitchState(State from, State to) {
			var was = (State)Interlocked.CompareExchange(ref _state, (int)to, (int)from);
			if (was != from)
				throw new InvalidOperationException($"Failed to transition object from state: {from} to {to}. Was {was}.");
		}
	}

	public interface IReusableObject {
		void Initialize(IReusableObjectInitParams initParams);
		void Reset();
	}

	public interface IReusableObjectInitParams { }
}
