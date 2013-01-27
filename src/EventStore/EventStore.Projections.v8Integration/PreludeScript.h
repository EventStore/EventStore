#pragma once
#include "js1.h"
#include "CompiledScript.h"
#include "QueryScript.h"
#include "ModuleScript.h"

namespace js1 {
	class ModuleScript;

	class PreludeScript : public CompiledScript 
	{
	public:
		PreludeScript(LOAD_MODULE_CALLBACK load_module_callback_, ENTER_CANCELLABLE_REGION enter_cancellable_region_callback_, 
			EXIT_CANCELLABLE_REGION exit_cancellable_region_callback_,  LOG_CALLBACK log_callback_) :
		isolate(v8::Isolate::New()), load_module_handler(load_module_callback_), 
			enter_cancellable_region_callback(enter_cancellable_region_callback_), 
			exit_cancellable_region_callback(exit_cancellable_region_callback_), log_handler(log_callback_) 
		{
			isolate_add_ref(isolate);
		}

		virtual ~PreludeScript();
		bool compile_script(const uint16_t *prelude_source, const uint16_t *prelude_file_name);
		bool try_run();
		v8::Persistent<v8::ObjectTemplate> get_template(std::vector<v8::Handle<v8::Value> > &prelude_arguments);

		bool enter_cancellable_region() { return enter_cancellable_region_callback(); }
		bool exit_cancellable_region();

	protected:
		virtual v8::Isolate *get_isolate();
		virtual v8::Persistent<v8::ObjectTemplate> create_global_template();

	private:
		v8::Isolate *isolate;
		v8::Persistent<v8::Function> global_template_factory;
		LOAD_MODULE_CALLBACK load_module_handler;
		LOG_CALLBACK log_handler;
		ENTER_CANCELLABLE_REGION enter_cancellable_region_callback;
		EXIT_CANCELLABLE_REGION exit_cancellable_region_callback;
		ModuleScript *load_module(uint16_t *module_name);

		static v8::Handle<v8::Value> log_callback(const v8::Arguments& args); 
		static v8::Handle<v8::Value> load_module_callback(const v8::Arguments& args); 
	};


}
