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

using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture]
    internal class appending_to_explicitly_created_stream
    {
        public void sequence_0_1e0_2e1_3e2_4e3_5e4_6e5_1e0_idempotent()
        {
        }

        public void sequence_0_1e0_2e1_3e2_4e3_5e4_6e5_1any_idempotent()
        {
        }

        public void sequence_0_1e0_2e1_3e2_4e3_5e4_6e5_1e6_non_idempotent()
        {
        }

        public void sequence_0_1e0_2e1_3e2_4e3_5e4_6e5_1e7_wev()
        {
        }

        public void sequence_0_1e0_2e1_3e2_4e3_5e4_6e5_1e5_wev()
        {
        }

        public void sequence_0_1e0_1e1_non_idempotent()
        {
        }

        public void sequence_0_1e0_1any_idempotent()
        {
        }

        public void sequence_0_1e0_1e0_idempotent()
        {
        }

        public void sequence_0_1e0_2e1_3e2_2any_2any_idempotent()
        {
        }

        public void sequence_0_S_1e0_2e0_E_S_1e0_E_idempotent()
        {
        }

        public void sequence_0_S_1e0_2e0_E_S_1any_E_idempotent()
        {
        }

        public void sequence_0_S_1e0_2e0_E_S_2e0_E_idempotent()
        {
        }

        public void sequence_0_S_1e0_2e0_E_S_2any_E_idempotent()
        {
        }

        public void sequence_0_S_1e0_2e0_E_S_1e0_2e0_3e0_E_idempotancy_fail()
        {
        }
    }
}
