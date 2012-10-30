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

	bool ModuleScript::compile_script(const uint16_t *source, const uint16_t *file_name)
	{
		return CompiledScript::compile_script(source, file_name);
	}

	void ModuleScript::run()
	{
		v8::Context::Scope context_scope(get_context());
		module_object.Dispose();
		module_object.Clear();

		v8::Handle<v8::Value> result = run_script(get_context());
		if (result->IsObject())
		{
			module_object = v8::Persistent<v8::Object>::New(result.As<v8::Object>());
		}
		else 
		{
			set_last_error(v8::String::New("Module script must return an object"));
		}
	}

	v8::Handle<v8::Object> ModuleScript::get_module_object()
	{
		return module_object;
	}

	v8::Isolate *ModuleScript::get_isolate()
	{
		return isolate;
	}

	v8::Persistent<v8::ObjectTemplate> ModuleScript::create_global_template() 
	{

		if (prelude == NULL) 
		{
			// prelude can be NULL if $load_module invoked from the prelude defintion itself
			return v8::Persistent<v8::ObjectTemplate>::New(v8::ObjectTemplate::New());
		}

		//TODO: make sure prelude script handles module requests (i.e. without any parameters)
		std::vector<v8::Handle<v8::Value> > arguments(0);
		return prelude->get_template(arguments);
	}

}

