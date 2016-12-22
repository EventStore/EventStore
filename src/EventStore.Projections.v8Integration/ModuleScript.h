#pragma once
#include "js1.h"
#include "CompiledScript.h"
#include "PreludeScript.h"
#include "V8Wrapper.h"

namespace js1 {

	class ModuleScript : public CompiledScript 
	{
	public:
		ModuleScript(v8::Isolate *isolate_, PreludeScript *prelude_) :
			isolate(isolate_), 
			prelude(prelude_) 
		{
			js1::V8Wrapper::Instance().isolate_add_ref(isolate);
		};

		virtual ~ModuleScript();
		virtual v8::Isolate *get_isolate();
		virtual v8::Handle<v8::Context> get_context();

		Status compile_script(const uint16_t *module_source, const uint16_t *module_file_name);
		Status try_run();

		v8::Handle<v8::Object> get_module_object();


	protected:
		virtual Status create_global_template(v8::Handle<v8::ObjectTemplate> &result);

	private:
		v8::Isolate *isolate;
		PreludeScript *prelude;
		std::shared_ptr<v8::Persistent<v8::Object>> module_object;
	};
}