#pragma once

namespace js1 
{

	class EventHandler 
	{
	public:
		EventHandler(v8::Handle<v8::String> _name, v8::Handle<v8::Function> _handler):
			name(v8::Persistent<v8::String>::New(_name)),
			handler(v8::Persistent<v8::Function>::New(_handler))
		{
		}

		~EventHandler()
		{
			name.Dispose();
			handler.Dispose();
		}

		v8::Handle<v8::Function> get_handler()
		{
			return handler;
		}

	private:
		v8::Persistent<v8::String> name;
		v8::Persistent<v8::Function> handler;

		EventHandler(const EventHandler &source){} // do not allow making copies
		EventHandler & operator=(const EventHandler &right){} // do not allow assignments
	};



}