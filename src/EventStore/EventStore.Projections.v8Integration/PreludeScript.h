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
		Status compile_script(const uint16_t *prelude_source, const uint16_t *prelude_file_name);
		Status try_run();
		Status get_template(std::vector<v8::Handle<v8::Value> > &prelude_arguments, v8::Handle<v8::ObjectTemplate> &result);

		bool enter_cancellable_region() { return enter_cancellable_region_callback(); }
		bool exit_cancellable_region();

	protected:
		virtual v8::Isolate *get_isolate();
		virtual Status create_global_template(v8::Handle<v8::ObjectTemplate> &result);

	private:
		v8::Isolate *isolate;
		std::shared_ptr<v8::Persistent<v8::Function>> global_template_factory;
		LOAD_MODULE_CALLBACK load_module_handler;
		LOG_CALLBACK log_handler;
		ENTER_CANCELLABLE_REGION enter_cancellable_region_callback;
		EXIT_CANCELLABLE_REGION exit_cancellable_region_callback;
		ModuleScript *load_module(uint16_t *module_name);

		static void log_callback(const v8::FunctionCallbackInfo<v8::Value>& info); 
		static void load_module_callback(const v8::FunctionCallbackInfo<v8::Value>& info); 
	};


}
