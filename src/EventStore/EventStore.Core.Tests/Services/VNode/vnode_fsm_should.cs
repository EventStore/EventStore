using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;
using EventStore.Core.Services.VNode;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.VNode
{
    internal abstract class P: Message {}
    internal class A: P {}
    internal class B: P {}
    internal class C: Message {}

    [TestFixture]
    public class vnode_fsm_should
    {
        [Test]
        public void allow_ignoring_messages_by_common_ancestor()
        {
            var fsm = new VNodeFSMBuilder(() => VNodeState.Master)
                    .InAnyState()
                    .When<P>().Ignore()
                    .WhenOther().Do(x => Assert.Fail("{0} slipped through", x.GetType().Name))
                    .Build();

            fsm.Handle(new A());
            fsm.Handle(new B());
        }

        [Test]
        public void handle_specific_message_even_if_base_message_is_ignored()
        {
            bool aHandled = false;
            var fsm = new VNodeFSMBuilder(() => VNodeState.Master)
                    .InAnyState()
                    .When<P>().Ignore()
                    .When<A>().Do(x => aHandled = true)
                    .WhenOther().Do(x => Assert.Fail("{0} slipped through", x.GetType().Name))
                    .Build();

            fsm.Handle(new A());
            fsm.Handle(new B());

            Assert.IsTrue(aHandled);
        }

    }
}
