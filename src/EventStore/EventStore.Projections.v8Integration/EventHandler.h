#pragma once

namespace js1 
{

	class EventHandler 
	{
	public:
		EventHandler(const v8::Handle<v8::String>& _name, const v8::Handle<v8::Function>& _handler):
			name(Create(_name)),
			handler(Create(_handler))
		{
		}

		v8::Handle<v8::Function> get_handler()
		{
			return v8::Handle<v8::Function>::New(v8::Isolate::GetCurrent(), *handler);
		}

	private:
		std::shared_ptr<v8::Persistent<v8::String>> name;
		std::shared_ptr<v8::Persistent<v8::Function>> handler;

		EventHandler(const EventHandler &source){} // do not allow making copies
		EventHandler & operator=(const EventHandler &right){} // do not allow assignments

		static std::shared_ptr<v8::Persistent<v8::Function>> Create(v8::Handle<v8::Function> source) 
		{
			return std::shared_ptr<v8::Persistent<v8::Function>>(new v8::Persistent<v8::Function>(v8::Isolate::GetCurrent(), source));
		}

		static std::shared_ptr<v8::Persistent<v8::String>> Create(v8::Handle<v8::String> source) 
		{
			return std::shared_ptr<v8::Persistent<v8::String>>(new v8::Persistent<v8::String>(v8::Isolate::GetCurrent(), source));
		}
	};



}