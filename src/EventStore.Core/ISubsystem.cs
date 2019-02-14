using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventStore.Core {
	public interface ISubsystem {
		void Register(StandardComponents standardComponents);

		IEnumerable<Task> Start();
		void Stop();
	}
}
