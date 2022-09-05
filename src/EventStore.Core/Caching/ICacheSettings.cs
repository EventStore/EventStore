//qq seen
using System;

namespace EventStore.Core.Caching {
	//qq is the interface necessary, might there be other implementations?
	//qq what do some of these properties mean
	//qq especially do some of these apply only to dynamic/static
	//  i wonder if dynamic/static should be subclasses
	// ahh some of these are read by the dynamic cache manager and some are _set_ by it
	public interface ICacheSettings {
		string Name { get; }
		bool IsDynamic { get; }
		int Weight { get; }

		//qq bit odd that some of these are setters, are they used?
		// static => this is the static allowance
		// dynamic => this starts as -1 and is populated by the manager
		long InitialMaxMemAllocation { get; set; }

		long MinMemAllocation { get; }
		//qq bit odd that some of these are setters, are they used?

		//qqq if this just gets the number of bytes for values in the cache
		// then the cost of the cache infra itself goes in the memory that the 
		// caches are supposed to leave free
		// static and dynamic, called by manager for stats and resizing
		// set by indexbackend
		Func<long> GetMemoryUsage { get; set; }

		//qq bit odd that some of these are setters, are they used?
		// static => not called
		// dynamic => called by manager
		// set by index backend
		Action<long> UpdateMaxMemoryAllocation { get; set; }

		//qq for dynamic the cache itself finds out its allotment via
		// InitialMaxMemAllocation and UpdateMaxMemoryAllocation

		//qqq tripping over the word 'allocation' a bit, i think here we dont mean
		// the amount of space allocated on the heap, but how much space has been
		// allocated to the cache by the manager. perhaps we should call it allowence
		// and then in the manger _maxMemAllocation might be better named 
		// _allowances. 

	}
}
