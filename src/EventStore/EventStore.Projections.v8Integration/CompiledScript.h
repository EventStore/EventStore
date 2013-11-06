#pragma once
#include "js1.h"

#include <memory>

namespace js1 {

	enum Status { S_OK = 0, S_ERROR = 1, S_TERMINATED = 2};

	class CompiledScript {
	public:
		friend class PreludeScope;
		CompiledScript();
		virtual void report_errors(REPORT_ERROR_CALLBACK report_error_callback);
		void isolate_terminate_execution();
	protected:
		virtual v8::Isolate *get_isolate() = 0;
		virtual Status create_global_template(v8::Handle<v8::ObjectTemplate> &result) = 0;

		v8::Handle<v8::Context> get_context();
		Status compile_script(const uint16_t *source, const uint16_t *file_name);
		v8::Handle<v8::Value> run_script(v8::Handle<v8::Context> context);
		bool set_last_error(bool is_error, v8::TryCatch &try_catch);
		void set_last_error(v8::Handle<v8::String> message);
		static void isolate_add_ref(v8::Isolate * isolate);
		static size_t isolate_release(v8::Isolate * isolate);
	private:
		std::shared_ptr<v8::Persistent<v8::ObjectTemplate>> global;
		std::shared_ptr<v8::Persistent<v8::Context>> context;
		std::shared_ptr<v8::Persistent<v8::Script>> script;
		std::shared_ptr<v8::Persistent<v8::Value>> last_exception;
	};

}