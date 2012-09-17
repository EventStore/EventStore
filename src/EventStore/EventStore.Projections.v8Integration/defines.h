#pragma once

// The following ifdef block is the standard way of creating macros which make exporting 
// from a DLL simpler. All files within this DLL are compiled with the JS1_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see 
// JS1_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#if __GNUC__ >= 4
  #if __amd64__
    #define JS1_API __attribute__ ((visibility ("default")))
    #define STDCALL 
  #else
    #define JS1_API __attribute__ ((visibility ("default"))) 
    #define STDCALL __attribute__((stdcall))
  #endif
  #define THREADSTATIC __thread
#else
  #define STDCALL __stdcall
  #ifdef JS1_EXPORTS
  #define JS1_API __declspec(dllexport) 
  #else
  #define JS1_API __declspec(dllimport) 
  #endif
  #define THREADSTATIC __declspec(thread) 
#endif

