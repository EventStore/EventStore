using System;
using System.Linq;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class AllUserDetailsResultHttpFormatted
    {

        public readonly UserDataHttpFormated[] Data;
        public readonly bool Success;
        public readonly UserManagementMessage.Error Error;
        public readonly int MsgTypeId;

        public AllUserDetailsResultHttpFormatted(UserManagementMessage.AllUserDetailsResult msg, Func<string, string> makeAbsoluteUrl)
        {
            Data = msg.Data.Select(user => new UserDataHttpFormated(user, makeAbsoluteUrl)).ToArray();
            MsgTypeId = msg.MsgTypeId;
            Success = msg.Success;
            Error = msg.Error;
        }

    }
}
