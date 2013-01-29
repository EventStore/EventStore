#include "stdafx.h"
#include "PreludeScope.h"
#include "CompiledScript.h"
#include "ModuleScript.h"
#include "QueryScript.h"
#include "EventHandler.h"


using namespace v8;

namespace js1 
{

	ModuleScript::~ModuleScript()
	{
		module_object.Dispose();
		isolate_release(isolate);
	}

	Status ModuleScript::compile_script(const uint16_t *source, const uint16_t *file_name)
	{
		return CompiledScript::compile_script(source, file_name);
	}

	Status ModuleScript::try_run()
	{
		v8::Context::Scope context_scope(get_context());
		module_object.Dispose();
		module_object.Clear();

		if (prelude != NULL)
			if (!prelude->enter_cancellable_region()) 
				return S_TERMINATED;
		v8::Handle<v8::Value> result = run_script(get_context());
		if (prelude != NULL) 
			if (!prelude->exit_cancellable_region())
				return S_TERMINATED;

		if (result->IsObject())
		{
			module_object = v8::Persistent<v8::Object>::New(result.As<v8::Object>());
		}
		else 
		{
			set_last_error(v8::String::New("Module script must return an object"));
		}
		return S_OK;
	}

	v8::Handle<v8::Object> ModuleScript::get_module_object()
	{
		return module_object;
	}

	v8::Isolate *ModuleScript::get_isolate()
	{
		return isolate;
	}

	Status ModuleScript::create_global_template(v8::Persistent<v8::ObjectTemplate> &result) 
	{

		if (prelude == NULL) 
		{
			// prelude can be NULL if $load_module invoked from the prelude defintion itself
			result = v8::Persistent<v8::ObjectTemplate>::New(v8::ObjectTemplate::New());
			return S_OK;
		}

		//TODO: make sure prelude script handles module requests (i.e. without any parameters)
		std::vector<v8::Handle<v8::Value> > arguments(0);
		return prelude->get_template(arguments, result);
	}

}

