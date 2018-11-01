// js1.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "js1.h"
#include "V8Wrapper.h"
#include "CompiledScript.h"
#include "PreludeScript.h"
#include "QueryScript.h"

extern "C" 
{
	JS1_API int js1_api_version()
	{
		v8::Isolate *isolate = js1::V8Wrapper::Instance().create_isolate();
		// NOTE: this also verifies whether this build can work at all
		{
			v8::Isolate::Scope isolate_scope(isolate);
			v8::HandleScope scope(isolate);
			v8::Handle<v8::Context> context = v8::Context::New(isolate);
			v8::TryCatch try_catch(isolate);
		}
		return 1;
	}

	//TODO: revise error reporting - it is no the best way to create faulted objects and then immediately dispose them
	JS1_API void * STDCALL compile_module(void *prelude, const uint16_t *script, const uint16_t *file_name)
	{
		js1::PreludeScript *prelude_script = reinterpret_cast<js1::PreludeScript *>(prelude);
		js1::ModuleScript *module_script;

		v8::HandleScope handle_scope(prelude_script->get_isolate());

		module_script = new js1::ModuleScript(prelude_script->get_isolate(), prelude_script);

		js1::Status status;
		if ((status = module_script->compile_script(script, file_name)) == js1::S_OK){
			status = module_script->try_run();
		}

		if (status != js1::S_TERMINATED){
			return module_script;
		}

		delete module_script;
		return NULL;
	};

	JS1_API void * STDCALL compile_prelude(const uint16_t *prelude, const uint16_t *file_name, LOAD_MODULE_CALLBACK load_module_callback, 
		ENTER_CANCELLABLE_REGION enter_cancellable_region_callback, EXIT_CANCELLABLE_REGION exit_cancellable_region_callback, LOG_CALLBACK log_callback)
	{
		js1::PreludeScript *prelude_script;
		v8::Isolate *isolate = js1::V8Wrapper::Instance().create_isolate();
		{
			prelude_script = new js1::PreludeScript(isolate, load_module_callback, enter_cancellable_region_callback, exit_cancellable_region_callback, log_callback);

			v8::HandleScope handle_scope(isolate);

			js1::Status status;
			if ((status = prelude_script->compile_script(prelude, file_name)) == js1::S_OK) {
				status = prelude_script->try_run();
			}

			if (status != js1::S_TERMINATED) {
				return prelude_script;
			}
		}
		delete prelude_script;
		return NULL;
	};

	JS1_API void * STDCALL compile_query(
		void *prelude, 
		const uint16_t *script,
		const uint16_t *file_name,
		REGISTER_COMMAND_HANDLER_CALLBACK register_command_handler_callback,
		REVERSE_COMMAND_CALLBACK reverse_command_callback
		)
	{
		js1::PreludeScript *prelude_script = reinterpret_cast<js1::PreludeScript *>(prelude);
		js1::QueryScript *query_script;
		v8::HandleScope handle_scope(prelude_script->get_isolate());

		query_script = new js1::QueryScript(prelude_script, prelude_script->get_isolate(), register_command_handler_callback, reverse_command_callback);

		js1::Status status;
		if ((status = query_script->compile_script(script, file_name)) == js1::S_OK){
			status = query_script->try_run();
		}

		if (status != js1::S_TERMINATED){
			return query_script;
		}

		delete query_script;
		return NULL;

	};

	JS1_API void STDCALL dispose_script(void *script_handle)
	{
		js1::CompiledScript *compiled_script;
		compiled_script = reinterpret_cast<js1::CompiledScript *>(script_handle);
		delete compiled_script;
	};

	JS1_API bool STDCALL execute_command_handler(void *script_handle, void* event_handler_handle, const uint16_t *data_json, 
		const uint16_t *data_other[], int32_t other_length, uint16_t **result_json, uint16_t **result2_json, void **memory_handle)
	{
		js1::QueryScript *query_script;
		//TODO: add v8::try_catch here (and move scope/context to this level) and make errors reportable to the C# level
		query_script = reinterpret_cast<js1::QueryScript *>(script_handle);

		v8::Isolate::Scope isolate_scope(query_script->get_isolate());
		v8::HandleScope handle_scope(query_script->get_isolate());

		v8::Handle<v8::String> result;
		v8::Handle<v8::String> result2;
		js1::Status success = query_script->
			execute_handler(event_handler_handle, data_json, data_other, other_length, result, result2);

		if (success != js1::S_OK) {
			*result_json = NULL;
			*memory_handle = NULL;
			return false;
		}
		//NOTE: incorrect return types are handled in execute_handler
		if (!result.IsEmpty()) 
		{
			v8::String::Value * result_buffer = new v8::String::Value(query_script->get_isolate(),result);
			v8::String::Value * result2_buffer = new v8::String::Value(query_script->get_isolate(),result2);
			*result_json = **result_buffer;
			*result2_json = **result2_buffer;

			void** handles = new void*[2];

			handles[0] = result_buffer;
			handles[1] = result2_buffer;
			*memory_handle = handles;
		}
		else 
		{
			*result_json = NULL;
			*memory_handle = NULL;
		}
		return true;
	};

	JS1_API void STDCALL free_result(void *result)
	{
		if (!result)
			return;

		void **memory_handles = (void**)result;

		v8::String::Value * result_buffer = reinterpret_cast<v8::String::Value *>(memory_handles[0]);
		delete result_buffer;

		v8::String::Value * result2_buffer = reinterpret_cast<v8::String::Value *>(memory_handles[1]);
		if (result2_buffer)
			delete result2_buffer;

		delete memory_handles;
	};

	JS1_API void STDCALL terminate_execution(void *script_handle)
	{
		js1::QueryScript *query_script;
		query_script = reinterpret_cast<js1::QueryScript *>(script_handle);

		query_script->isolate_terminate_execution();
	};

	JS1_API void report_errors(void *script_handle, REPORT_ERROR_CALLBACK report_error_callback) 
	{
		js1::QueryScript *query_script;
		query_script = reinterpret_cast<js1::QueryScript *>(script_handle);
		v8::HandleScope handle_scope(query_script->get_isolate());
		query_script->report_errors(query_script->get_isolate(), query_script->get_context(), report_error_callback);
	}
}

