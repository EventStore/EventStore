using System;
using EventStore.Core.Data;

namespace EventStore.Core {
	public class VNodeStatusChangeArgs : EventArgs {
		public readonly VNodeState NewVNodeState;

		public VNodeStatusChangeArgs(VNodeState newVNodeState) {
			NewVNodeState = newVNodeState;
		}
	}
}
