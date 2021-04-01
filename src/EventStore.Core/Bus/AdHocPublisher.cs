using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	public class AdHocPublisher : IPublisher {
		public Action<Message> Target { set { if(_target != null){ throw new NotImplementedException("Cannot Change Target");} _target = value; } }
		private Action<Message> _target;
		public AdHocPublisher(Action<Message> target) {
			_target = target;
		}	
		public void Publish(Message message) {
			if(_target == null){ throw new NotImplementedException("Target not set.");}
			_target(message);
		}
	}
}
