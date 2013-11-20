// js1.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "js1.h"
#include "CompiledScript.h"
#include "PreludeScript.h"
#include "QueryScript.h"
#include "PreludeScope.h"

extern "C" 
{
	JS1_API int js1_api_version()
	{
		v8::Isolate *isolate = v8::Isolate::New();
		isolate->Enter();
		// NOTE: this also verifies whether this build can work at all
		{
			v8::HandleScope scope(v8::Isolate::GetCurrent());
			v8::Handle<v8::Context> context = v8::Context::New(v8::Isolate::GetCurrent());
			v8::TryCatch try_catch;
		}
		isolate->Exit();
		isolate->Dispose();
		return 1;
	}

	//TODO: revise error reporting - it is no the best way to create faulted objects and then immediately dispose them
	JS1_API void * STDCALL compile_module(void *prelude, const uint16_t *script, const uint16_t *file_name)
	{
		js1::PreludeScript *prelude_script = reinterpret_cast<js1::PreludeScript *>(prelude);
		js1::ModuleScript *module_script;

		js1::PreludeScope prelude_scope(prelude_script);
		v8::HandleScope scope(v8::Isolate::GetCurrent());


		module_script = new js1::ModuleScript(prelude_script);

		js1::Status status;
		if ((status = module_script->compile_script(script, file_name)) == js1::S_OK)
			status = module_script->try_run();

		if (status != js1::S_TERMINATED)
			return module_script;

		delete module_script;
		return NULL;
	};

	JS1_API void * STDCALL compile_prelude(const uint16_t *prelude, const uint16_t *file_name, LOAD_MODULE_CALLBACK load_module_callback, 
		ENTER_CANCELLABLE_REGION enter_calcellable_region_callback, EXIT_CANCELLABLE_REGION exit_cancellable_region_callback, LOG_CALLBACK log_callback)
	{
		js1::PreludeScript *prelude_script;
		prelude_script = new js1::PreludeScript(load_module_callback, enter_calcellable_region_callback, exit_cancellable_region_callback, log_callback);
		js1::PreludeScope prelude_scope(prelude_script);

		v8::HandleScope scope(v8::Isolate::GetCurrent());

		js1::Status status;
		if ((status = prelude_script->compile_script(prelude, file_name)) == js1::S_OK)
			status = prelude_script->try_run();

		if (status != js1::S_TERMINATED)
			return prelude_script;

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
		js1::PreludeScope prelude_scope(prelude_script);

		v8::HandleScope scope(v8::Isolate::GetCurrent());


		query_script = new js1::QueryScript(prelude_script, register_command_handler_callback, reverse_command_callback);

		js1::Status status;
		if ((status = query_script->compile_script(script, file_name)) == js1::S_OK)
			status = query_script->try_run();

		if (status != js1::S_TERMINATED)
			return query_script;

		delete query_script;
		return NULL;

	};

	JS1_API void STDCALL dispose_script(void *script_handle)
	{
		js1::CompiledScript *compiled_script;
		compiled_script = reinterpret_cast<js1::CompiledScript *>(script_handle);
		js1::PreludeScope prelude_scope(compiled_script);
		delete compiled_script;
	};

	JS1_API bool STDCALL execute_command_handler(void *script_handle, void* event_handler_handle, const uint16_t *data_json, 
		const uint16_t *data_other[], int32_t other_length, uint16_t **result_json, void **memory_handle)
	{

		js1::QueryScript *query_script;
		//TODO: add v8::try_catch here (and move scope/context to this level) and make errors reportable to theC# level
		
		query_script = reinterpret_cast<js1::QueryScript *>(script_handle);
		js1::PreludeScope prelude_scope(query_script);

		v8::HandleScope handle_scope(v8::Isolate::GetCurrent());

		v8::Handle<v8::String> result;
		js1::Status success = query_script->execute_handler(event_handler_handle, data_json, data_other, other_length, result);
		if (success != js1::S_OK) {
			*result_json = NULL;
			*memory_handle = NULL;
			return false;
		}
		//NOTE: incorrect return types are handled in execute_handler
		if (!result.IsEmpty()) 
		{
			v8::String::Value * result_buffer = new v8::String::Value(result);
			*result_json = **result_buffer;
			*memory_handle = result_buffer;
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
		v8::String::Value * result_buffer = reinterpret_cast<v8::String::Value *>(result);		
		delete result_buffer;
	};

	JS1_API void STDCALL terminate_execution(void *script_handle)
	{
		js1::QueryScript *query_script;
		query_script = reinterpret_cast<js1::QueryScript *>(script_handle);

		query_script->isolate_terminate_execution();
	};

	//TODO: revise error reporting completely (we are loosing error messages from the load_module this way)
	JS1_API void report_errors(void *script_handle, REPORT_ERROR_CALLBACK report_error_callback) 
	{
		js1::QueryScript *query_script;
		query_script = reinterpret_cast<js1::QueryScript *>(script_handle);
		js1::PreludeScope prelude_scope(query_script);

		query_script->report_errors(report_error_callback);
	}
}

