extern alias GrpcClient;
using System.Net;
using EventStore.Core.Services;
using UserCredentials = GrpcClient::EventStore.Client.UserCredentials;

namespace EventStore.Core.Tests {
	public class DefaultData {
		public static string AdminUsername = SystemUsers.Admin;
		public static string AdminPassword = SystemUsers.DefaultAdminPassword;
		public static UserCredentials AdminCredentials = new UserCredentials(AdminUsername, AdminPassword);
		public static NetworkCredential AdminNetworkCredentials = new NetworkCredential(AdminUsername, AdminPassword);
		public static ClusterVNodeOptions.DefaultUserOptions DefaultUserOptions = new ClusterVNodeOptions.DefaultUserOptions() {
			DefaultAdminPassword = SystemUsers.DefaultAdminPassword,
			DefaultOpsPassword = SystemUsers.DefaultOpsPassword
		};
	}
}
