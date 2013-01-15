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
namespace EventStore.TestClient.Commands.DvuBasic
{
    public static class BankAccountEventFactory
    {
        public static object CreateAccountObject(int version)
        {
            object accountObject = null;

            {
                const int checkpointVersion = 10;

                var checkPointModVersion = version % checkpointVersion;
                if (checkPointModVersion == 0)
                {
                    int otherCheckPointsCount = version / checkpointVersion;

                    var elementsCount = version / 2;

                    var creditedSum = ComputeSum(20, elementsCount, 20) - ComputeSum(100, otherCheckPointsCount, 100);
                    var debitedSum = ComputeSum(10, elementsCount, 20);

                    var checkpoint = new AccountCheckPoint(creditedSum, debitedSum);

                    accountObject = checkpoint;
                }
                else
                {
                    var modVersion = version % 2;
                    if (modVersion == 0)
                    {
                        var credited = new AccountCredited(version * 10, version % 17);
                        accountObject = credited;
                    }
                    else
                    {
                        var debited = new AccountDebited(version * 10, version % 17);
                        accountObject = debited;
                    }
                }
            }
            return accountObject;
        }

        private static int ComputeSum(int first, int count, int step)
        {
            var sum = count * (2 * first + step * (count - 1)) / 2;
            return sum;
        }
    }
}