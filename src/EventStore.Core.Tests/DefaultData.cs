using System.Net;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;

namespace EventStore.Core.Tests {
	public class DefaultData {
		public static string AdminUsername = SystemUsers.Admin;
		public static string AdminPassword = SystemUsers.DefaultAdminPassword;
		public static UserCredentials AdminCredentials = new UserCredentials(AdminUsername, AdminPassword);
		public static NetworkCredential AdminNetworkCredentials = new NetworkCredential(AdminUsername, AdminPassword);
	}
}
