#pragma once
#include "js1.h"
#include "CompiledScript.h"
#include "PreludeScript.h"

namespace js1 {

	class EventHandler;
	class QueryScript;
	class PreludeScript;

	class QueryScriptScope {
	public:
		QueryScriptScope(QueryScript *query_script);
		~QueryScriptScope();
		static QueryScript &Current();

	private:
		static THREADSTATIC QueryScript *current;
	};

	class QueryScript : public CompiledScript
	{
	public:
		QueryScript(
			PreludeScript *prelude_, 
			REGISTER_COMMAND_HANDLER_CALLBACK register_command_handler_callback_, 
			REVERSE_COMMAND_CALLBACK reverse_command_callback_) : 
		
			prelude(prelude_), 
			register_command_handler_callback(register_command_handler_callback_),
			reverse_command_callback(reverse_command_callback_)

			{};

		virtual ~QueryScript();

		bool compile_script(const uint16_t *query_source, const uint16_t *file_name);
		v8::Handle<v8::Value> run();
		v8::Persistent<v8::String> execute_handler(void* event_handler_handle, const uint16_t *data_json, const uint16_t *data_other[], int32_t other_length);

	protected:
		virtual v8::Persistent<v8::ObjectTemplate> create_global_template();

	private:
		std::list<EventHandler *> registred_handlers;
		REGISTER_COMMAND_HANDLER_CALLBACK register_command_handler_callback;
		REVERSE_COMMAND_CALLBACK reverse_command_callback;

		PreludeScript *prelude;

		v8::Handle<v8::Value> on(const v8::Arguments& args);
		v8::Handle<v8::Value> notify(const v8::Arguments& args);

		static v8::Handle<v8::Value> on_callback(const v8::Arguments& args); 
		static v8::Handle<v8::Value> notify_callback(const v8::Arguments& args); 

	};
}
