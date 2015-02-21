using System;
using System.Collections.Generic;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class UserDataHttpFormated
    {
        public readonly string LoginName;
        public readonly string FullName;
        public readonly string[] Groups;
        public readonly DateTimeOffset? DateLastUpdated;
        public readonly bool Disabled;
        public readonly List<RelLink> Links;

        public UserDataHttpFormated(UserManagementMessage.UserData userData, Func<string, string> makeAbsoluteUrl)
        {
            LoginName = userData.LoginName;
            FullName = userData.FullName;
            Groups = userData.Groups;
            Disabled = userData.Disabled;

            Links = new List<RelLink>();
            var userLocalUrl = "/users/" + userData.LoginName;
            Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl + "/command/reset-password"), "reset-password"));
            Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl + "/command/change-password"), "change-password"));
            Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl), "edit"));
            Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl), "delete"));

            if (userData.Disabled)
            {
                Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl + "/command/enable"), "enable"));
            }
            else
            {
                Links.Add(new RelLink(makeAbsoluteUrl(userLocalUrl + "/command/disable"), "disable"));
            }
        }
    }
}
