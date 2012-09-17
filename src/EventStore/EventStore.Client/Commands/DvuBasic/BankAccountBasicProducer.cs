// Copyright (c) 2012, Event Store Ltd
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
// Neither the name of the Event Store Ltd nor the names of its
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
using System;
using System.Text;
using EventStore.Common.Log;
using EventStore.Core.Data;
using EventStore.Core.Services.Transport.Http;

namespace EventStore.TestClient.Commands.DvuBasic
{
    public class BankAccountBasicProducer : IBasicProducer
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<BankAccountBasicProducer>();

        public string Name
        {
            get
            {
                return "account";
            }
        }

        public Event Create(int version)
        {
            var accountObject = BankAccountEventFactory.CreateAccountObject(version);

            var serializedObject = Codec.Json.To(accountObject);
            var @event = new Event(Guid.NewGuid(), accountObject.GetType().FullName, false,  Encoding.UTF8.GetBytes(serializedObject), new byte[0]);

            return @event;
        }

        public bool Equal(int eventVersion, string eventType, byte[] actualData)
        {
            var generated = BankAccountEventFactory.CreateAccountObject(eventVersion);
            object deserialized;

            bool isEqual;
            string reason;
            if (actualData == null)
            {
                isEqual = false;
                reason = "Null received";
            }
            else
            {
                deserialized = Deserialize(eventType, actualData);

                if (deserialized.GetType() != generated.GetType())
                {
                    isEqual = false;
                    reason = string.Format("Type does not match, actual type is {0}", deserialized.GetType().FullName);
                }
                else
                {
                    isEqual = generated.Equals(deserialized);
                    reason = "Value differs";
                }
            }

            if (!isEqual)
            {
                LogExpected(generated, actualData, reason);
            }

            return isEqual;
        }

        private object Deserialize(string eventType, byte[] actualData)
        {
            object result = null;
            var strData = Encoding.UTF8.GetString(actualData);
            if (eventType == typeof(AccountCreated).FullName)
            {
                result = Codec.Json.From<AccountCreated>(strData);
            }
            else
            {
                if (eventType == typeof(AccountCredited).FullName)
                {
                    result = Codec.Json.From<AccountCredited>(strData);
                }
                else
                {
                    if (eventType == typeof(AccountDebited).FullName)
                    {
                        result = Codec.Json.From<AccountDebited>(strData);
                    }
                    else
                    {
                        if (eventType == typeof(AccountCheckPoint).FullName)
                        {
                            result = Codec.Json.From<AccountCheckPoint>(strData);
                        }
                        else
                        {
                            throw new NotSupportedException(string.Format("Event type {0} is not recognized.", eventType));
                        }
                    }

                }
            }
            return result;
        }

        private static void LogExpected(object generated, object actual, string reason)
        {
            Log.Info("Expected: {0}\n" +
                     "  Actual: {1}\n" +
                     " Details: {2}",
                     generated.ToString(),
                     (actual == null ? "<null>" : actual.ToString()),
                     reason
                );
        }
      
    }

    public class AccountCreated
    {
        public readonly string AccountNumber;

        public AccountCreated(string accountNumber)
        {
            AccountNumber = accountNumber;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (obj.GetType() != GetType())
                return false;

            var casted = (AccountCreated)obj;
            return Equals(casted);
        }

        protected bool Equals(AccountCreated other)
        {
            return string.Equals(AccountNumber, other.AccountNumber);
        }

        public override int GetHashCode()
        {
            return (AccountNumber != null ? AccountNumber.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return string.Format("Type: {0}; AccountNumber: {1}", GetType().Name, AccountNumber);
        }
    }

    public class AccountCredited
    {
        public readonly decimal CreditedAmount;

        public AccountCredited(decimal creditedAmount)
        {
            CreditedAmount = creditedAmount;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (obj.GetType() != GetType())
                return false;

            var casted = (AccountCredited)obj;
            return Equals(casted);
        }

        protected bool Equals(AccountCredited other)
        {
            return CreditedAmount == other.CreditedAmount;
        }

        public override int GetHashCode()
        {
            return CreditedAmount.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("Type: {0}; CreditedAmount: {1}", GetType().Name, CreditedAmount);
        }
    }

    public class AccountDebited
    {
        public readonly decimal DebitedAmount;

        public AccountDebited(decimal debitedAmount)
        {
            DebitedAmount = debitedAmount;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (obj.GetType() != GetType())
                return false;

            var casted = (AccountDebited)obj;
            return Equals(casted);
        }

        protected bool Equals(AccountDebited other)
        {
            return DebitedAmount == other.DebitedAmount;
        }

        public override int GetHashCode()
        {
            return DebitedAmount.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("Type: {0}; DebitedAmount: {1}", GetType().Name, DebitedAmount);
        }
    }

    public class AccountCheckPoint
    {
        public readonly decimal CreditedAmount;
        public readonly decimal DebitedAmount;

        public AccountCheckPoint(decimal creditedAmount, decimal debitedAmount)
        {
            CreditedAmount = creditedAmount;
            DebitedAmount = debitedAmount;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (obj.GetType() != GetType())
                return false;

            var casted = (AccountCheckPoint)obj;
            return Equals(casted);
        }

        protected bool Equals(AccountCheckPoint other)
        {
            return CreditedAmount == other.CreditedAmount && DebitedAmount == other.DebitedAmount;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (CreditedAmount.GetHashCode() * 397) ^ DebitedAmount.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("Type: {0}; CreditedAmount: {1}, DebitedAmount: {2}",
                                 GetType().Name,
                                 CreditedAmount,
                                 DebitedAmount);
        }
    }
}