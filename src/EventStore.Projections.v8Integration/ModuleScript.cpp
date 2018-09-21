#include "stdafx.h"
#include "CompiledScript.h"
#include "ModuleScript.h"
#include "QueryScript.h"
#include "EventHandler.h"


using namespace v8;

namespace js1 
{
	ModuleScript::~ModuleScript()
	{
		js1::V8Wrapper::Instance().isolate_release(isolate);
	}

	Status ModuleScript::compile_script(const uint16_t *source, const uint16_t *file_name)
	{
		return CompiledScript::compile_script(prelude->get_context(), prelude->get_object_template(), source, file_name);
	}

	Status ModuleScript::try_run()
	{
		v8::Isolate::Scope isolate_scope(get_isolate());
		v8::Context::Scope context_scope(prelude->get_context());
		module_object.reset();
		v8::Handle<v8::Value> result = run_script(get_isolate(), prelude->get_context());
		if (result->IsObject())
		{
			module_object = std::shared_ptr<v8::Persistent<v8::Object>>(
				new v8::Persistent<v8::Object>(get_isolate(), result.As<v8::Object>()));
		}
		else 
		{
			set_last_error(get_isolate(), "Module script must return an object");
			return S_ERROR;
		}
		return S_OK;
	}

	v8::Handle<v8::Object> ModuleScript::get_module_object()
	{
		return v8::Handle<v8::Object>::New(get_isolate(), *module_object);
	}

	v8::Isolate *ModuleScript::get_isolate()
	{
		return isolate;
	}

	v8::Handle<v8::Context> ModuleScript::get_context()
	{
		return prelude->get_context();
	}

	Status ModuleScript::create_global_template(v8::Handle<v8::ObjectTemplate> &result) 
	{
		if (prelude != NULL) 
		{
			// prelude can be NULL if $load_module invoked from the prelude defintion itself
			result = v8::ObjectTemplate::New(get_isolate());
			return S_OK;
		}

		//TODO: make sure prelude script handles module requests (i.e. without any parameters)
		std::vector<v8::Handle<v8::Value> > arguments(0);
		return prelude->get_template(arguments, result);
	}
}

