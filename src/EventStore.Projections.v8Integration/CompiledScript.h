#pragma once
#include "js1.h"

#include <memory>

namespace js1 {

	enum Status { S_OK = 0, S_ERROR = 1, S_TERMINATED = 2};

	class CompiledScript {
	public:
		CompiledScript();
		virtual ~CompiledScript();
		virtual void report_errors(v8::Isolate *isolate, v8::Handle<v8::Context> context, REPORT_ERROR_CALLBACK report_error_callback);
		void isolate_terminate_execution();

	protected:
		virtual v8::Isolate *get_isolate() = 0;
		virtual v8::Handle<v8::Context> get_context() = 0;
		virtual Status create_global_template(v8::Handle<v8::ObjectTemplate> &result) = 0;

		Status compile_script(v8::Handle<v8::Context> context, v8::Handle<v8::ObjectTemplate> object_template, const uint16_t *source, const uint16_t *file_name);
		v8::Handle<v8::Value> run_script(v8::Isolate* isolate, v8::Handle<v8::Context> context);
		bool set_last_error(v8::Isolate *isolate, bool is_error, v8::TryCatch &try_catch);
		void set_last_error(v8::Isolate *isolate, v8::Handle<v8::String> message);
		void set_last_error(v8::Isolate *isolate, const char *message);

	private:
		std::shared_ptr<v8::Persistent<v8::Script>> script;
		std::shared_ptr<v8::Persistent<v8::Value>> last_exception;
	};

}
