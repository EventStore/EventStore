#include "stdafx.h"
#include "V8Wrapper.h"

namespace js1
{
	V8Wrapper::~V8Wrapper(){
		v8::V8::Dispose();
		v8::V8::ShutdownPlatform();
		delete platform;
	};

	void V8Wrapper::initialize_v8(){
		v8::V8::InitializeICU();
		platform = v8::platform::CreateDefaultPlatform();
		v8::V8::InitializePlatform(platform);
		v8::V8::Initialize();
	};

	v8::Isolate *V8Wrapper::create_isolate(){
		ArrayBufferAllocator *allocator = new ArrayBufferAllocator();
		v8::Isolate::CreateParams create_params;
		create_params.array_buffer_allocator = allocator;
		v8::Isolate *isolate = v8::Isolate::New(create_params);
		return isolate;
	};
	
	void V8Wrapper::isolate_add_ref(v8::Isolate * isolate)
	{
		size_t counter = reinterpret_cast<size_t>(isolate->GetData(0));
		counter++;
		isolate->SetData(0, reinterpret_cast<void *>(counter));
	};

	size_t V8Wrapper::isolate_release(v8::Isolate * isolate) 
	{
		size_t counter = reinterpret_cast<size_t>(isolate->GetData(0));
		counter--;
		isolate->SetData(0, reinterpret_cast<void *>(counter));
		if(counter == 0){
			isolate->Dispose();
		}
		return counter;
	};
}