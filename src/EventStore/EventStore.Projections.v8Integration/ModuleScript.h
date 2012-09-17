#pragma once
#include "js1.h"
#include "CompiledScript.h"
#include "PreludeScript.h"

namespace js1 {

	class ModuleScript : CompiledScript 
	{
	public:
		ModuleScript(PreludeScript *prelude_) :
			prelude(prelude_) {};

		virtual ~ModuleScript();

		bool compile_script(const uint16_t *module_source, const uint16_t *module_file_name);
		void run();

		v8::Handle<v8::Object> get_module_object();

	protected:
		virtual v8::Persistent<v8::ObjectTemplate> create_global_template();

	private:
		PreludeScript *prelude;
		v8::Persistent<v8::Object> module_object;
	};


}