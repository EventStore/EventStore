#include "stdafx.h"
#include "CompiledScript.h"
#include "PreludeScript.h"
#include "QueryScript.h"
#include "EventHandler.h"

#include <string>


using namespace v8;

namespace js1 
{

	QueryScript::~QueryScript()
	{
		for (std::list<EventHandler *>::iterator it = registred_handlers.begin(); it != registred_handlers.end(); it++)
		{
			delete *it;
		}
		js1::V8Wrapper::Instance().isolate_release(isolate);
	}

	void QueryScript::report_errors(v8::Isolate *isolate, v8::Handle<v8::Context> context, REPORT_ERROR_CALLBACK report_error_callback)
	{
		v8::Isolate::Scope isolate_scope(isolate);
		v8::Context::Scope context_scope(context);
		prelude->report_errors(isolate, context, report_error_callback);
		CompiledScript::report_errors(isolate, context, report_error_callback);
	}

	Status QueryScript::compile_script(const uint16_t *script_source, const uint16_t *file_name)
	{
		return CompiledScript::compile_script(prelude->get_context(), prelude->get_object_template(), script_source, file_name);

	}

	Status QueryScript::try_run() 
	{
		if (!prelude->enter_cancellable_region())
			return S_TERMINATED;
		v8::Isolate::Scope isolate_scope(get_isolate());
		v8::Context::Scope context_scope(prelude->get_context());
		v8::Handle<v8::Value> result = run_script(get_isolate(), prelude->get_context());
		if (!prelude->exit_cancellable_region())
			return S_TERMINATED;

		return S_OK;
	}

	Status QueryScript::execute_handler(void *event_handler_handle, const uint16_t *data_json, 
		const uint16_t *data_other[], int32_t other_length, v8::Handle<v8::String> &result,
			v8::Handle<v8::String> &result2) 
	{
		EventHandler *event_handler = reinterpret_cast<EventHandler *>(event_handler_handle);

		v8::Context::Scope local(prelude->get_context());

		v8::Handle<v8::String> data_json_handle = v8::String::NewFromTwoByte(get_isolate(), data_json);
		v8::Handle<v8::Value> argv[10];
		argv[0] = data_json_handle;

		for (int i = 0; i < other_length; i++) {
			v8::Handle<v8::String> data_other_handle = v8::String::NewFromTwoByte(get_isolate(), data_other[i]);
			argv[1 + i] = data_other_handle;
		}

		v8::Handle<v8::Object> global = prelude->get_context()->Global();

		v8::TryCatch try_catch(isolate);

		if (!prelude->enter_cancellable_region())
		{
			printf ("Terminated? (1)");
			return S_TERMINATED;
		}
		v8::Handle<v8::Value> call_result = event_handler->get_handler()->Call(global, 1 + other_length, argv);
		if (!prelude->exit_cancellable_region())
		{
			printf ("Terminated? (2)");
			return S_TERMINATED;
		}

		bool empty_result = call_result.IsEmpty();

		if (set_last_error(get_isolate(), empty_result, try_catch))
			return S_ERROR;
		v8::Handle<v8::String> empty;
		if (!try_catch.Exception().IsEmpty())
		{
			result = empty;
			return S_ERROR;
		}

		if (call_result->IsArray()) 
		{
			v8::Handle<v8::Array> array_result = call_result.As<v8::Array>();
			Status status = GetStringValue(array_result->Get(0), result);
			if (status != S_OK) 
				return status;

			return GetStringValue(array_result->Get(1), result2);
		}
		else 
		{
			return GetStringValue(call_result, result);
		}
	}

	v8::Isolate *QueryScript::get_isolate()
	{
		return isolate;
	}

	v8::Handle<v8::Context> QueryScript::get_context()
	{
		return prelude->get_context();
	}

	Status QueryScript::create_global_template(v8::Handle<v8::ObjectTemplate> &result)
	{
		v8::Isolate::Scope isolate_scope(get_isolate());
		v8::Context::Scope context_scope(prelude->get_context());

		v8::Handle<v8::Value> query_script_wrap = v8::External::New(get_isolate(), this);

		std::vector<v8::Handle<v8::Value> > arguments(2);
		arguments[0] = v8::FunctionTemplate::New(get_isolate(), on_callback, query_script_wrap)->GetFunction();
		arguments[1] = v8::FunctionTemplate::New(get_isolate(), notify_callback, query_script_wrap)->GetFunction();

		Status status = prelude->get_template(arguments, result);
		if (status != S_OK)
			return status;
		return S_OK;
	}

	Status QueryScript::GetStringValue(v8::Handle<v8::Value> call_result, v8::Handle<v8::String> &result) 
	{
		v8::Handle<v8::String> empty;
		if (call_result->IsNull()) 
		{
			result.Clear();
			return S_OK;
		}
		if (!call_result->IsString()) {
			set_last_error(get_isolate(), "Handler must return string data or null");
			result = empty;
			return S_ERROR;
		}
		result = call_result.As<v8::String>();
		return S_OK;
		
	}

	void QueryScript::on(const v8::FunctionCallbackInfo<v8::Value>& args) 
	{
		if (args.Length() != 2) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'on' handler expects 2 arguments"))));
			return;
		}

		if (args[0].IsEmpty() || args[1].IsEmpty()) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'on' handler argument cannot be empty"))));
			return;
		}
		if (!args[0]->IsString()) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'on' handler first argument must be a string"))));
			return;
		}

		if (!args[1]->IsFunction()) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'on' handler second argument must be a function"))));
			return;
		}

		v8::Handle<v8::String> name(args[0].As<v8::String>());
		v8::Handle<v8::Function> handler(args[1].As<v8::Function>());
		EventHandler *event_handler = new EventHandler(name, handler);
		registred_handlers.push_back(event_handler);
		v8::String::Value uname(v8::Isolate::GetCurrent(),name);
		this->register_command_handler_callback(*uname, event_handler);
		args.GetReturnValue().Set(v8::Undefined(v8::Isolate::GetCurrent()));
	}

	void QueryScript::notify(const v8::FunctionCallbackInfo<v8::Value>& args) 
	{
		if (args.Length() != 2) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'notify' handler expects 2 arguments"))));
			return;
		}

		if (args[0].IsEmpty() || args[1].IsEmpty()) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'notify' handler argument cannot be empty"))));
			return;
		}

		if (!args[0]->IsString()) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'notify' handler first argument must be a string"))));
		}

		if (!args[1]->IsString()) 
		{
			args.GetReturnValue().Set(v8::Isolate::GetCurrent()->ThrowException(
				v8::Exception::Error(v8::String::NewFromUtf8(v8::Isolate::GetCurrent(), "The 'notify' handler second argument must be a string"))));
			return;
		}

		v8::Handle<v8::String> name(args[0].As<v8::String>());
		v8::Handle<v8::String> body(args[1].As<v8::String>());

		v8::String::Value name_value(v8::Isolate::GetCurrent(),name);
		v8::String::Value body_value(v8::Isolate::GetCurrent(),body);

		this->reverse_command_callback(*name_value, *body_value);

		args.GetReturnValue().Set(v8::Undefined(v8::Isolate::GetCurrent()));
	}

	void QueryScript::on_callback(const v8::FunctionCallbackInfo<v8::Value>& args) 
	{
		v8::Handle<v8::External> data = args.Data().As<v8::External>();
		QueryScript *query_script = reinterpret_cast<QueryScript *>(data->Value());
		return query_script->on(args);
	};

	void QueryScript::notify_callback(const v8::FunctionCallbackInfo<v8::Value>& args) 
	{
		v8::Handle<v8::External> data = args.Data().As<v8::External>();
		QueryScript *query_script = reinterpret_cast<QueryScript *>(data->Value());
		return query_script->notify(args);
	};


}

