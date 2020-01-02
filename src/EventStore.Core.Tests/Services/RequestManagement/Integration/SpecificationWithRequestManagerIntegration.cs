using System;
using System.Collections.Generic;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Integration;
using EventStore.Core.Tests.Services.Replication;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RequestManagement.Integration {
	public abstract class SpecificationWithRequestManagerIntegration : SpecificationWithIntegrationVNode {
		protected List<Message> Produced = new List<Message>();
		
		protected Guid InternalCorrId = Guid.NewGuid();
		protected Guid ClientCorrId = Guid.NewGuid();
		protected byte[] Metadata = new byte[255];
		protected byte[] EventData = new byte[255];
		protected FakeEnvelope Envelope;

		protected abstract IEnumerable<Message> WithInitialMessages();
		protected abstract Message When();

		[SetUp]
		public void Setup() {
			CreateTestNode();
			Envelope = new FakeEnvelope();
			
			foreach (var m in WithInitialMessages()) {
				Publish(m);
			}
			Subscribe(new AdHocHandler<Message>(Produced.Add));
			Publish(When());			
		}

	}
}
