using System.Security.Principal;

namespace EventStore.Core.Authentication
{
	public class CachedPrincipal
	{
		public readonly string HashedPassword;
		public readonly string PasswordSalt;
		public readonly IPrincipal Principal;

		public CachedPrincipal(string hashedPassword, string passwordSalt, IPrincipal principal)
		{
			HashedPassword = hashedPassword;
			PasswordSalt = passwordSalt;
			Principal = principal;
		}
	}
}