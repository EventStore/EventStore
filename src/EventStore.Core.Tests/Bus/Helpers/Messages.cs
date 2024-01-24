using System;
using EventStore.Core.Messaging;
using EventStore.Core.Scanning;

namespace EventStore.Core.Tests.Bus.Helpers {
	public class DeferredExecutionTestMessage : Message<DeferredExecutionTestMessage> {
		private static readonly int TypeId = SequenceId.Next();

		public new int MsgTypeId => TypeId;

		private readonly Action _action;

		public DeferredExecutionTestMessage(Action action) {
			if (action == null)
				throw new ArgumentNullException("action");
			_action = action;
		}

		public void Execute() {
			_action();
		}
	}

	public class ExecutableTestMessage : Message<ExecutableTestMessage> {
		private static readonly int TypeId = SequenceId.Next();

		public new int MsgTypeId => TypeId;

		private readonly Action _action;

		public ExecutableTestMessage(Action action) {
			if (action == null)
				throw new ArgumentNullException("action");
			_action = action;
		}

		public void Execute() {
			_action();
		}
	}
}
