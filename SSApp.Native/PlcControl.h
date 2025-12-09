#pragma once

#ifdef SSAPPNATIVE_EXPORTS
#define SSAPPNATIVE_API __declspec(dllexport)
#else
#define SSAPPNATIVE_API __declspec(dllimport)
#endif

extern "C" SSAPPNATIVE_API void StartScanNative(const char* ipAddress, int port);
