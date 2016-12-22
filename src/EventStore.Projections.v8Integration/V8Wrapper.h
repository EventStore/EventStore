#pragma once
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "js1.h"

namespace js1 
{
	class ArrayBufferAllocator : public v8::ArrayBuffer::Allocator {
	 public:
	  virtual void* Allocate(size_t length) {
	    void* data = AllocateUninitialized(length);
	    return data == NULL ? data : memset(data, 0, length);
	  }
	  virtual void* AllocateUninitialized(size_t length) { return malloc(length); }
	  virtual void Free(void* data, size_t) { free(data); }
	};
	class V8Wrapper
	{
		public:
			static V8Wrapper &Instance()
			{
			    static V8Wrapper instance;
			    return instance;
			}
			void initialize_v8();
			v8::Isolate *create_isolate();
			v8::Platform *platform;
			static void isolate_add_ref(v8::Isolate * isolate);
			static size_t isolate_release(v8::Isolate * isolate);
		protected:
			V8Wrapper()
			{
				initialize_v8();
			}
			~V8Wrapper();
	};
}