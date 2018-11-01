#include "stdafx.h"
#include "CompiledScript.h"
#include "EventHandler.h"

#include <string>


using namespace v8;

namespace js1 
{
	CompiledScript::CompiledScript()
	{
	}

    CompiledScript::~CompiledScript()
    {
    }

	void CompiledScript::isolate_terminate_execution() 
	{
		v8::Isolate* isolate = get_isolate();
		isolate->TerminateExecution();
	}

	void CompiledScript::report_errors(v8::Isolate *isolate, v8::Handle<v8::Context> context, REPORT_ERROR_CALLBACK report_error_callback)
	{
		v8::Isolate::Scope isolate_scope(isolate);
		if (isolate->IsDead() || isolate->IsExecutionTerminating())
		{
			//TODO: define error codes
			report_error_callback(2, NULL);
			return;
		}

		if (last_exception && !last_exception->IsEmpty()) 
		{
			v8::HandleScope handle_scope(isolate);
			v8::Context::Scope local(context);

			v8::String::Value error_value(isolate,v8::Handle<v8::Value>::New(isolate, *last_exception));
			//TODO: define error codes
			report_error_callback(1, *error_value);
		}
	}

	Status CompiledScript::compile_script(v8::Handle<v8::Context> context, v8::Handle<v8::ObjectTemplate> object_template, const uint16_t *script_source, const uint16_t *file_name)
	{
		v8::Isolate::Scope isolate_scope(get_isolate());
		v8::HandleScope handle_scope(get_isolate());
		v8::Context::Scope context_scope(context);

		Status status = create_global_template(object_template);

		if (status != S_OK)
			return status;

		v8::TryCatch try_catch(get_isolate());
		v8::ScriptOrigin script_origin = v8::ScriptOrigin(v8::String::NewFromTwoByte(get_isolate(), file_name));
		v8::MaybeLocal<v8::Script> result = v8::Script::Compile(
			context,
			v8::String::NewFromTwoByte(get_isolate(), script_source),
			&script_origin);

		if(result.IsEmpty()){
			set_last_error(get_isolate(), true, try_catch);
			return S_ERROR;
		}

		v8::Handle<v8::Script> resultChecked = result.ToLocalChecked();
		if (set_last_error(get_isolate(), resultChecked.IsEmpty(), try_catch))
			return S_ERROR;

		if (resultChecked.IsEmpty())
			return S_ERROR;

		script = std::shared_ptr<v8::Persistent<v8::Script>>(
			new v8::Persistent<v8::Script>(get_isolate(), resultChecked));

		return S_OK;
	}

	v8::Handle<v8::Value> CompiledScript::run_script(v8::Isolate *isolate, v8::Handle<v8::Context> context)
	{
		v8::TryCatch try_catch(get_isolate());
		v8::MaybeLocal<v8::Value> result = v8::Handle<v8::Script>::New(isolate, *script)->Run(context);

		if(result.IsEmpty()){
			set_last_error(isolate, true, try_catch);
			return v8::Handle<v8::Value>();
		}

		v8::Handle<v8::Value> resultChecked = result.ToLocalChecked();
		if (set_last_error(isolate, resultChecked.IsEmpty(), try_catch)){
			resultChecked.Clear();
		}
		return resultChecked;
	}

	bool CompiledScript::set_last_error(v8::Isolate *isolate, bool is_error, v8::TryCatch &try_catch)
	{
		if (!is_error && !try_catch.Exception().IsEmpty()) {
			set_last_error(isolate, "Caught exception which was not indicated as an error");
			return true;
		}
		if (is_error) 
		{
			Handle<Value> exception = try_catch.Exception();
			last_exception.reset();
			last_exception = std::shared_ptr<v8::Persistent<v8::Value>>(
				new v8::Persistent<v8::Value>(isolate, exception));
			return true;
		}
		else 
		{
			last_exception.reset();
			return false;
		}
	}

	void CompiledScript::set_last_error(v8::Isolate *isolate, v8::Handle<v8::String> message)
	{
		Handle<Value> exception = v8::Exception::Error(message);
		last_exception.reset();
		last_exception = std::shared_ptr<v8::Persistent<v8::Value>>(
			new v8::Persistent<v8::Value>(isolate, exception));
	}

	void CompiledScript::set_last_error(v8::Isolate *isolate, const char *message)
	{
		set_last_error(isolate, v8::String::NewFromUtf8(isolate, message));
	}
}

