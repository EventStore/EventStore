#pragma once
#include "js1.h"
#include "CompiledScript.h"
#include "PreludeScript.h"

namespace js1 {

	class EventHandler;
	class QueryScript;
	class PreludeScript;

	class QueryScript : public CompiledScript
	{
	public:
		QueryScript(
			PreludeScript *prelude_, 
			REGISTER_COMMAND_HANDLER_CALLBACK register_command_handler_callback_, 
			REVERSE_COMMAND_CALLBACK reverse_command_callback_) : 
		
			isolate(v8::Isolate::GetCurrent()),
			prelude(prelude_), 
			register_command_handler_callback(register_command_handler_callback_),
			reverse_command_callback(reverse_command_callback_)

		{
			isolate_add_ref(isolate);
		};

		virtual ~QueryScript();
		virtual void report_errors(REPORT_ERROR_CALLBACK report_error_callback);

		Status compile_script(const uint16_t *query_source, const uint16_t *file_name);
		Status try_run();
		Status execute_handler(void* event_handler_handle, const uint16_t *data_json, 
			const uint16_t *data_other[], int32_t other_length, v8::Handle<v8::String> &result,
			v8::Handle<v8::String> &result2);
	protected:
		virtual v8::Isolate *get_isolate();
		virtual Status create_global_template(v8::Handle<v8::ObjectTemplate> &result);

	private:
		v8::Isolate *isolate;
		std::list<EventHandler *> registred_handlers;
		REGISTER_COMMAND_HANDLER_CALLBACK register_command_handler_callback;
		REVERSE_COMMAND_CALLBACK reverse_command_callback;

		PreludeScript *prelude;

		Status QueryScript::GetStringValue(v8::Handle<v8::Value> call_result, v8::Handle<v8::String> &result);

		void on(const v8::FunctionCallbackInfo<v8::Value>& info);
		void notify(const v8::FunctionCallbackInfo<v8::Value>& info);

		static void on_callback(const v8::FunctionCallbackInfo<v8::Value>& info); 
		static void notify_callback(const v8::FunctionCallbackInfo<v8::Value>& info); 

	};
}
