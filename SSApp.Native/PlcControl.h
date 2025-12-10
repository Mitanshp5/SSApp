#pragma once

#ifdef SSAPPNATIVE_EXPORTS
#define SSAPPNATIVE_API __declspec(dllexport)
#else
#define SSAPPNATIVE_API __declspec(dllimport)
#endif

extern "C" {

    SSAPPNATIVE_API void StartScanNative(const char* ipAddress, int port);

    SSAPPNATIVE_API bool ConnectPlc(const char* ipAddress, int port);

    SSAPPNATIVE_API void DisconnectPlc();

    SSAPPNATIVE_API int GetLastPlcValue(); // Returns value of D0

    SSAPPNATIVE_API bool GetIsConnected(); // Returns true if connected

}
