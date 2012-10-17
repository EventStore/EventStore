#pragma once
#include "js1.h"

namespace js1 {

	class CompiledScript {
	public:
		friend class PreludeScope;
		CompiledScript();
		virtual ~CompiledScript();
		void report_errors(REPORT_ERROR_CALLBACK report_error_callback);
		virtual bool owns_isolate() { return false; }
	protected:
		virtual v8::Isolate *get_isolate() = 0;
		virtual v8::Persistent<v8::ObjectTemplate> create_global_template() = 0;

		v8::Persistent<v8::Context> &get_context();
		bool compile_script(const uint16_t *source, const uint16_t *file_name);
		v8::Handle<v8::Value> run_script(v8::Persistent<v8::Context> context);
		void set_last_error(bool is_error, v8::TryCatch &try_catch);
		void set_last_error(v8::Handle<v8::String> message);
	private:
		v8::Persistent<v8::ObjectTemplate> global;
		v8::Persistent<v8::Context> context;
		v8::Persistent<v8::Script> script;
		v8::Persistent<v8::Value> last_exception;
	};

}