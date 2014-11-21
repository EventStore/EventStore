namespace EventStore.ClientAPI.UserManagement
{
    using EventStore.ClientAPI.Common.Utils;

    internal class UserUpdateInformation
    {
        public readonly string FullName;

        public readonly string[] Groups;

        public UserUpdateInformation(string fullName, string[] groups)
        {
            Ensure.NotNullOrEmpty(fullName, "fullName");
            Ensure.NotNull(groups, "fullName");
            FullName = fullName;
            Groups = groups;
        }
    }
}