using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.UserManagement
{
    internal class ChangePasswordDetails
    {
        public readonly string OldPassword;

        public readonly string NewPassword;

        public ChangePasswordDetails(string oldPassword, string newPassword)
        {
            Ensure.NotNullOrEmpty(oldPassword, "oldPassword");
            Ensure.NotNullOrEmpty(newPassword, "newPassword");
            this.OldPassword = oldPassword;
            this.NewPassword = newPassword;
        }
    }
}
