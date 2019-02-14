namespace EventStore.ClientAPI.UserManagement {
	/// <summary>
	/// 
	/// </summary>
	public class RelLink {
		/// <summary>
		/// 
		/// </summary>
		public readonly string Href;

		/// <summary>
		/// 
		/// </summary>
		public readonly string Rel;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="href"></param>
		/// <param name="rel"></param>
		public RelLink(string href, string rel) {
			Href = href;
			Rel = rel;
		}
	}
}
