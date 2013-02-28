#include "stdafx.h"
#include "PreludeScope.h"
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
		isolate_release(isolate);
	}

	void QueryScript::report_errors(REPORT_ERROR_CALLBACK report_error_callback)
	{
		CompiledScript::report_errors(report_error_callback);
		prelude->report_errors(report_error_callback);
	}

	Status QueryScript::compile_script(const uint16_t *script_source, const uint16_t *file_name)
	{
		this->register_command_handler_callback = register_command_handler_callback;

		return CompiledScript::compile_script(script_source, file_name);

	}

	Status QueryScript::try_run() 
	{
		if (!prelude->enter_cancellable_region())
			return S_TERMINATED;

		v8:Handle<v8::Value> result = run_script(get_context());
		if (!prelude->exit_cancellable_region())
			return S_TERMINATED;

		return S_OK;
	}

	Status QueryScript::execute_handler(void *event_handler_handle, const uint16_t *data_json, 
		const uint16_t *data_other[], int32_t other_length, v8::Persistent<v8::String> &result) 
	{
		EventHandler *event_handler = reinterpret_cast<EventHandler *>(event_handler_handle);

		v8::HandleScope handle_scope;
		v8::Context::Scope local(get_context());

		v8::Handle<v8::String> data_json_handle = v8::String::New(data_json);
		v8::Handle<v8::Value> argv[10];
		argv[0] = data_json_handle;

		for (int i = 0; i < other_length; i++) {
			v8::Handle<v8::String> data_other_handle = v8::String::New(data_other[i]);
			argv[1 + i] = data_other_handle;
		}

		v8::Handle<v8::Object> global = get_context()->Global();

		v8::TryCatch try_catch;

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

		if (set_last_error(call_result.IsEmpty(), try_catch))
			return S_ERROR;
		v8::Handle<v8::String> empty;
		if (!try_catch.Exception().IsEmpty())
		{
			result = v8::Persistent<v8::String>::New(empty);
			return S_ERROR;
		}
		if (call_result->IsNull()) 
		{
			result.Clear();
			return S_OK;
		}
		if (!call_result->IsString()) {
			set_last_error(v8::String::New("Handler must return string data or null"));
			result = v8::Persistent<v8::String>::New(empty);
			return S_ERROR;
		}
		result = v8::Persistent<v8::String>::New(call_result.As<v8::String>());
		return S_OK;
	}

	v8::Isolate *QueryScript::get_isolate()
	{
		return isolate;
	}

	Status QueryScript::create_global_template(v8::Persistent<v8::ObjectTemplate> &result)
	{
		v8::Persistent<v8::Context> temp_context = v8::Context::New();
		v8::Context::Scope temp_context_scope(temp_context);

		v8::Handle<v8::Value> query_script_wrap = v8::External::New(this);

		std::vector<v8::Handle<v8::Value> > arguments(2);
		arguments[0] = v8::FunctionTemplate::New(on_callback, query_script_wrap)->GetFunction();
		arguments[1] = v8::FunctionTemplate::New(notify_callback, query_script_wrap)->GetFunction();

		Status status = prelude->get_template(arguments, result);
		if (status != S_OK)
			return status;
		temp_context.Dispose();
		return S_OK;
	}

	v8::Handle<v8::Value> QueryScript::on(const v8::Arguments& args) 
	{
		if (args.Length() != 2) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'on' handler expects 2 arguments")));

		if (args[0].IsEmpty() || args[1].IsEmpty()) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'on' handler argument cannot be empty")));

		if (!args[0]->IsString()) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'on' handler first argument must be a string")));

		if (!args[1]->IsFunction()) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'on' handler second argument must be a function")));

		v8::Handle<v8::String> name(args[0].As<v8::String>());
		v8::Handle<v8::Function> handler(args[1].As<v8::Function>());
		EventHandler *event_handler = new EventHandler(name, handler);
		registred_handlers.push_back(event_handler);
		v8::String::Value uname(name);
		this->register_command_handler_callback(*uname, event_handler);
		return v8::Undefined();
	}

	v8::Handle<v8::Value> QueryScript::notify(const v8::Arguments& args) 
	{
		if (args.Length() != 2) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'notify' handler expects 2 arguments")));

		if (args[0].IsEmpty() || args[1].IsEmpty()) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'notify' handler argument cannot be empty")));

		if (!args[0]->IsString()) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'notify' handler first argument must be a string")));

		if (!args[1]->IsString()) 
			return v8::ThrowException(v8::Exception::Error(v8::String::New("The 'notify' handler second argument must be a string")));

		v8::Handle<v8::String> name(args[0].As<v8::String>());
		v8::Handle<v8::String> body(args[1].As<v8::String>());

		v8::String::Value name_value(name);
		v8::String::Value body_value(body);

		this->reverse_command_callback(*name_value, *body_value);

		return v8::Undefined();
	}

	v8::Handle<v8::Value> QueryScript::on_callback(const v8::Arguments& args) 
	{
		v8::Handle<v8::External> data = args.Data().As<v8::External>();
		QueryScript *query_script = reinterpret_cast<QueryScript *>(data->Value());
		return query_script->on(args);
	};

	v8::Handle<v8::Value> QueryScript::notify_callback(const v8::Arguments& args) 
	{
		v8::Handle<v8::External> data = args.Data().As<v8::External>();
		QueryScript *query_script = reinterpret_cast<QueryScript *>(data->Value());
		return query_script->notify(args);
	};


}

