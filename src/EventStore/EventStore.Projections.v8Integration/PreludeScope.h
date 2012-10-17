#pragma once
#include "CompiledScript.h"

namespace js1 {

	class PreludeScope
	{
	public:
		// ignore null prelude script - likely from load module callback and isolate is already set
		PreludeScope(CompiledScript *prelude, bool deleting = false) :
			isolate(prelude ? prelude->get_isolate() : NULL), deleting(deleting)
		{
			if (isolate)
				isolate->Enter();
		}
		~PreludeScope()
		{
			if (isolate) {
				// do not exit if we havn't entered
				v8::Isolate *current = v8::Isolate::GetCurrent();
				if (current)
					current->Exit();
				if (deleting)
					current->Dispose();
			}
		}
	private:
		bool deleting;
		v8::Isolate *isolate;
		PreludeScope(const PreludeScope &);
		PreludeScope& operator=(const PreludeScope &);
	};
}
