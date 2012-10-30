#include "stdafx.h"
#include "PreludeScope.h"
#include "CompiledScript.h"
#include "PreludeScript.h"
#include "QueryScript.h"
#include "EventHandler.h"

namespace js1 
{

	PreludeScript::~PreludeScript()
	{
		global_template_factory.Dispose();
		isolate_release(isolate);
	}


	bool PreludeScript::compile_script(const uint16_t *prelude_source, const uint16_t *prelude_file_name)
	{
		return CompiledScript::compile_script(prelude_source, prelude_file_name);
	}

	bool PreludeScript::run()
	{
		v8::Context::Scope context_scope(get_context());
		global_template_factory.Dispose();
		global_template_factory.Clear();
		//TODO: check whether proper type of value returned
		v8::Handle<v8::Value> prelude_result = run_script(get_context());
		if (prelude_result.IsEmpty())
		{
			set_last_error(v8::String::New("Prelude script did not return any value"));
			return false;
		}
		if (prelude_result->IsFunction()) 
		{
			global_template_factory = v8::Persistent<v8::Function>::New(prelude_result.As<v8::Function>());
			return true;
		}
		else 
		{
			set_last_error(v8::String::New("Prelude script must return a function"));
			return false;
		}
	}

	v8::Persistent<v8::ObjectTemplate> PreludeScript::get_template(std::vector<v8::Handle<v8::Value> > &prelude_arguments)
	{
		v8::Context::Scope context_scope(get_context());
		v8::Handle<v8::Object> global = get_context()->Global();
		v8::Persistent<v8::ObjectTemplate> result;
		v8::Handle<v8::Value> prelude_result;
		v8::Handle<v8::Object> prelude_result_object;
		v8::TryCatch try_catch;
		prelude_result = global_template_factory->Call(global, (int)prelude_arguments.size(), prelude_arguments.data());
		set_last_error(prelude_result.IsEmpty(), try_catch);
		if (prelude_result.IsEmpty())
		{
			set_last_error(v8::String::New("Global template factory did not return any value"));
			return result; // initialized with 0 by default
		}
		if (!prelude_result->IsObject()) 
		{
			set_last_error(v8::String::New("Prelude script must return a function"));
			return result; // initialized with 0 by default
		}

		prelude_result_object = prelude_result.As<v8::Object>();
		result = v8::Persistent<v8::ObjectTemplate>::New(v8::ObjectTemplate::New());
		v8::Handle<v8::Array> global_property_names = prelude_result_object->GetPropertyNames();

		for (unsigned int i = 0; i < global_property_names->Length(); i++) 
		{
			//TODO: handle invalid keys in template object (non-string)
			v8::Handle<v8::String> global_property_name = global_property_names->Get(i).As<v8::String>();
			v8::Handle<v8::Value> global_property_value = prelude_result_object->Get(global_property_name);

			result->Set(global_property_name, global_property_value);
		}

		return result;
	}

	v8::Isolate *PreludeScript::get_isolate()
	{
		return isolate;
	}

	v8::Persistent<v8::ObjectTemplate> PreludeScript::create_global_template() 
	{
		//TODO: move actual callbacks out of this script into C# code
		v8::Persistent<v8::ObjectTemplate> prelude = v8::Persistent<v8::ObjectTemplate>::New(v8::ObjectTemplate::New());
		prelude->Set(v8::String::New("$log"), v8::FunctionTemplate::New(log_callback, v8::External::Wrap(this)));
		prelude->Set(v8::String::New("$load_module"), v8::FunctionTemplate::New(load_module_callback, v8::External::Wrap(this)));
		return prelude;
	}

	ModuleScript * PreludeScript::load_module(uint16_t *module_name)
	{
		// the C# load_module handler is expected to call back into C++ to compile module if necessary
		// this double callback is required to avoid memory management for strings returned from the C# part
		// string passed as arguments into C++ are much easy to handle

		void *module_handle = load_module_handler(module_name);
		return reinterpret_cast<ModuleScript *>(module_handle);
	}

	v8::Handle<v8::Value> PreludeScript::log_callback(const v8::Arguments& args) 
	{
		if (args.Length() != 1) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'log' handler expects 1 argument")));

		if (args[0].IsEmpty()) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'log' handler argument cannot be empty")));

		// TODO: do we need to check argument data type?

		v8::Handle<v8::Value> data = args.Data();
		PreludeScript *prelude = reinterpret_cast<PreludeScript *>(v8::External::Unwrap(data));

		//TODO: make sure correct value type passed
		v8::String::Value message(args[0].As<v8::String>());

		prelude->log_handler(*message);
		return v8::Undefined();
	};

	v8::Handle<v8::Value> PreludeScript::load_module_callback(const v8::Arguments& args) 
	{
		if (args.Length() != 1) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'load_module' handler expects 1 argument")));

		if (args[0].IsEmpty()) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'load_module' handler argument cannot be empty")));

		if (!args[0]->IsString()) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'load_module' handler argument must be a string")));

		v8::Handle<v8::Value> data = args.Data();
		PreludeScript *prelude = reinterpret_cast<PreludeScript *>(v8::External::Unwrap(data));

		//TODO: make sure correct value type passed
		v8::String::Value module_name(args[0].As<v8::String>());

		ModuleScript *module = prelude->load_module(*module_name);
		if (module == NULL)
			return v8::ThrowException(v8::String::New("Cannot load module"));
		return module->get_module_object();
	};

}

