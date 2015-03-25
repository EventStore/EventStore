using System;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class UserDetailsResultHttpFormatted
    {
        public readonly UserDataHttpFormated Data;
        public readonly bool Success;
        public readonly UserManagementMessage.Error Error;
        public readonly int MsgTypeId;

        public UserDetailsResultHttpFormatted(UserManagementMessage.UserDetailsResult msg, Func<string, string> makeAbsoluteUrl)
        {
            MsgTypeId = msg.MsgTypeId;
            if(msg.Data != null)
                Data = new UserDataHttpFormated(msg.Data, makeAbsoluteUrl);
            Success = msg.Success;
            Error = msg.Error;
        }

    }
}
