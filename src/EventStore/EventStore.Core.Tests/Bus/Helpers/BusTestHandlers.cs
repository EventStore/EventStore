// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Bus.Helpers
{
    public abstract class TestHandlerBase
    {
        public readonly List<Message> HandledMessages = new List<Message>();

        public bool DidntHandleAnyMessages()
        {
            return HandledMessages.Count == 0;
        }
    }

    public class TestMessageHandler<T> : IHandle<T> where T: Message
    {
        public readonly List<T> HandledMessages = new List<T>();

        public void Handle(T message)
        {
            HandledMessages.Add(message);
        }
    }

    public class TestHandler : TestHandlerBase, IHandle<TestMessage>
    {
        public void Handle(TestMessage message)
        {
            HandledMessages.Add(message);
        }
    }
    public class TestHandler2 : TestHandlerBase, IHandle<TestMessage2>
    {
        public void Handle(TestMessage2 message)
        {
            HandledMessages.Add(message);
        }
    }
    public class TestHandler3 : TestHandlerBase, IHandle<TestMessage3>
    {
        public void Handle(TestMessage3 message)
        {
            HandledMessages.Add(message);
        }
    }

    public class ParentTestHandler : TestHandlerBase, IHandle<ParentTestMessage>
    {
        public void Handle(ParentTestMessage message)
        {
            HandledMessages.Add(message);
        }
    }
    public class ChildTestHandler : TestHandlerBase, IHandle<ChildTestMessage>
    {
        public void Handle(ChildTestMessage message)
        {
            HandledMessages.Add(message);
        }
    }
    public class GrandChildTestHandler : TestHandlerBase, IHandle<GrandChildTestMessage>
    {
        public void Handle(GrandChildTestMessage message)
        {
            HandledMessages.Add(message);
        }
    }

    public class MultipleMessagesTestHandler : TestHandlerBase, IHandle<TestMessage>, IHandle<TestMessage2>, IHandle<TestMessage3>
    {
        public void Handle(TestMessage message)
        {
            HandledMessages.Add(message);
        }

        public void Handle(TestMessage2 message)
        {
            HandledMessages.Add(message);
        }

        public void Handle(TestMessage3 message)
        {
            HandledMessages.Add(message);
        }
    }

    public class SameMessageHandler1 : TestHandlerBase, IHandle<TestMessage>
    {
        public void Handle(TestMessage message)
        {
            HandledMessages.Add(message);
        }
    }
    public class SameMessageHandler2 : TestHandlerBase, IHandle<TestMessage>
    {
        public void Handle(TestMessage message)
        {
            HandledMessages.Add(message);
        }
    }

    public class MessageWithIdHandler : TestHandlerBase, IHandle<TestMessageWithId>
    {
        public void Handle(TestMessageWithId message)
        {
            HandledMessages.Add(message);
        }
    }


}
