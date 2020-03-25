using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Indicates which order of preferred nodes for connecting to.
	/// </summary>
	public enum NodePreference {
		/// <summary>
		/// When attempting connection, prefers master node.
		/// </summary>
		Master,

		/// <summary>
		/// When attempting connection, prefers slave node.
		/// </summary>
		Slave,

		/// <summary>
		/// When attempting connection, has no node preference.
		/// </summary>
		Random,
		
		/// <summary>
		/// When attempting connection, prefers read only replicas.
		/// </summary>
		ReadOnlyReplica
	}
}
