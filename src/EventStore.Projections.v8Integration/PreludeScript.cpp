#include "stdafx.h"
#include "CompiledScript.h"
#include "PreludeScript.h"
#include "QueryScript.h"
#include "EventHandler.h"

namespace js1 
{
	PreludeScript::~PreludeScript()
	{
		js1::V8Wrapper::Instance().isolate_release(isolate);
	}

	Status PreludeScript::compile_script(const uint16_t *prelude_source, const uint16_t *prelude_file_name)
	{
		return CompiledScript::compile_script(get_context(), get_object_template(), prelude_source, prelude_file_name);
	}

	Status PreludeScript::try_run()
	{
		v8::Isolate::Scope isolate_scope(get_isolate());
		v8::Context::Scope context_scope(get_context());
		global_template_factory.reset();

		if (!enter_cancellable_region()) 
			return S_TERMINATED;

		v8::Handle<v8::Value> prelude_result = run_script(get_isolate(), get_context());
		if (!exit_cancellable_region())
			return S_TERMINATED;

		if (prelude_result.IsEmpty()) 
		{
			set_last_error(get_isolate(), "Prelude script did not return any value");
			return S_ERROR;
		}
		if (!prelude_result->IsFunction()) 
		{
			set_last_error(get_isolate(), "Prelude script must return a function");
			return S_ERROR;
		}
		global_template_factory = std::shared_ptr<v8::Persistent<v8::Function>>(
			new v8::Persistent<v8::Function>(get_isolate(), prelude_result.As<v8::Function>()));
		return S_OK;
	}

	Status PreludeScript::get_template(std::vector<v8::Handle<v8::Value> > &prelude_arguments, v8::Handle<v8::ObjectTemplate> &result)
	{
		v8::Isolate::Scope isolate_scope(get_isolate());
		v8::Context::Scope context_scope(get_context());
		v8::Handle<v8::Object> global = get_context()->Global();
		v8::Handle<v8::Value> prelude_result;
		v8::Handle<v8::Object> prelude_result_object;
		v8::TryCatch try_catch(isolate);

		if (!enter_cancellable_region()) 
			return S_TERMINATED; // initialized with 0 by default
		v8::Handle<v8::Function> global_template_factory_local = v8::Handle<v8::Function>::New(get_isolate(), *global_template_factory);
		prelude_result = global_template_factory_local->Call(global, (int)prelude_arguments.size(), prelude_arguments.data());
		if (!exit_cancellable_region())
			return S_TERMINATED; // initialized with 0 by default

		if (set_last_error(get_isolate(), prelude_result.IsEmpty(), try_catch))
			return S_ERROR;
		if (prelude_result.IsEmpty())
		{
			set_last_error(get_isolate(), "Global template factory did not return any value");
			return S_ERROR; // initialized with 0 by default
		}
		if (!prelude_result->IsObject()) 
		{
			set_last_error(get_isolate(), "Prelude script must return a function");
			return S_ERROR; // initialized with 0 by default
		}

		prelude_result_object = prelude_result.As<v8::Object>();
		v8::Handle<v8::Array> global_property_names = prelude_result_object->GetPropertyNames();

		for (unsigned int i = 0; i < global_property_names->Length(); i++) 
		{
			v8::Handle<v8::String> global_property_name = global_property_names->Get(i).As<v8::String>();
			v8::Handle<v8::Value> global_property_value = prelude_result_object->Get(global_property_name);

			v8::String::Utf8Value name(get_isolate(),global_property_name);
			v8::String::Utf8Value value(get_isolate(),global_property_value);
			global->Set(global_property_name, global_property_value);
		}

		return S_OK;
	}

	bool PreludeScript::exit_cancellable_region() 
	{ 
		if (get_isolate()->IsExecutionTerminating())
		{
			printf("Terminating!");
		}
		return exit_cancellable_region_callback(); 
	}

	v8::Handle<v8::ObjectTemplate> PreludeScript::get_object_template()
	{
		return v8::Handle<v8::ObjectTemplate>::New(isolate, *global);
	}

	v8::Isolate *PreludeScript::get_isolate()
	{
		return isolate;
	}

	v8::Handle<v8::Context> PreludeScript::get_context()
	{
		return v8::Handle<v8::Context>::New(isolate, *context);
	}

	Status PreludeScript::create_global_template(v8::Handle<v8::ObjectTemplate> &result)
	{
		return S_OK;
	}

	ModuleScript * PreludeScript::load_module(uint16_t *module_name)
	{
		// the C# load_module handler is expected to call back into C++ to compile module if necessary
		// this double callback is required to avoid memory management for strings returned from the C# part
		// string passed as arguments into C++ are much easy to handle

		void *module_handle = load_module_handler(this, module_name);
		return reinterpret_cast<ModuleScript *>(module_handle);
	}

	void PreludeScript::log_callback(const v8::FunctionCallbackInfo<v8::Value>& args) 
	{
		if (args.Length() != 1)
		{
			args.GetReturnValue().Set(
				v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'log' handler expects 1 argument"))));
			return;
		}
		if (args[0].IsEmpty()) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'log' handler argument cannot be empty"))));
			return;
		}

		// TODO: do we need to check argument data type?

		v8::Handle<v8::External> data = args.Data().As<v8::External>();
		PreludeScript *prelude = reinterpret_cast<PreludeScript *>(data->Value());

		//TODO: make sure correct value type passed
		v8::String::Value message(v8::Isolate::GetCurrent(),args[0].As<v8::String>());

		prelude->log_handler(*message);
		args.GetReturnValue().Set(v8::Undefined(v8::Isolate::GetCurrent()));
		return;
	};

	void PreludeScript::load_module_callback(const v8::FunctionCallbackInfo<v8::Value>& args) 
	{
		if (args.Length() != 1) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'load_module' handler expects 1 argument"))));
			return;
		}

		if (args[0].IsEmpty()) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'load_module' handler argument cannot be empty"))));
			return;
		}

		if (!args[0]->IsString()) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'load_module' handler argument must be a string"))));
			return;
		}

		v8::Handle<v8::External> data = args.Data().As<v8::External>();
		PreludeScript *prelude = reinterpret_cast<PreludeScript *>(data->Value());

		//TODO: make sure correct value type passed
		v8::String::Value module_name(v8::Isolate::GetCurrent(),args[0].As<v8::String>());

		ModuleScript *module = prelude->load_module(*module_name);
		if (module == NULL) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "Cannot load module")));
			return;
		}
		args.GetReturnValue().Set(module->get_module_object());
	};

	void PreludeScript::init(v8::Isolate *isolate) {
		v8::Isolate::Scope isolate_scope(isolate);

		v8::HandleScope handle_scope(isolate);

		// Create global template
		global = std::shared_ptr<v8::Persistent<v8::ObjectTemplate>>(
			new v8::Persistent<v8::ObjectTemplate>(isolate, 
			v8::ObjectTemplate::New(isolate)));

		v8::Handle<v8::ObjectTemplate> global_local = v8::Handle<v8::ObjectTemplate>::New(
			isolate, *global);

		global_local->Set(v8::String::NewFromUtf8(isolate, "$log"), 
			v8::FunctionTemplate::New(isolate, log_callback, v8::External::New(isolate, this)));
		global_local->Set(v8::String::NewFromUtf8(isolate, "$load_module"), 
			v8::FunctionTemplate::New(isolate, load_module_callback, v8::External::New(isolate, this)));

		// Create a new context.
		context = std::shared_ptr<v8::Persistent<v8::Context>>(
			new v8::Persistent<v8::Context>(isolate, 
			v8::Context::New(isolate, NULL, global_local)));
	};
}

