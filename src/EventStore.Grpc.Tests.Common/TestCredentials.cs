namespace EventStore.Grpc {
	public static class TestCredentials {
		public static readonly UserCredentials Root = new UserCredentials("admin", "changeit");
		public static readonly UserCredentials TestUser1 = new UserCredentials("user1", "pa$$1");
		public static readonly UserCredentials TestUser2 = new UserCredentials("user2", "pa$$2");
		public static readonly UserCredentials TestAdmin = new UserCredentials("adm", "admpa$$");
		public static readonly UserCredentials TestBadUser = new UserCredentials("badlogin", "badpass");
	}
}
