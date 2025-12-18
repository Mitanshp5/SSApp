#pragma once

#ifdef SSAPPNATIVE_EXPORTS
#define SSAPPNATIVE_API __declspec(dllexport)
#else
#define SSAPPNATIVE_API __declspec(dllimport)
#endif

extern "C" {

    SSAPPNATIVE_API void StartScanNative(const char* ipAddress, int port);



    SSAPPNATIVE_API void StartLiveView(void* hWnd, int deviceIndex);

    SSAPPNATIVE_API void StopLiveView();

    SSAPPNATIVE_API int GetCameraCount();

    SSAPPNATIVE_API bool GetCameraName(int index, char* nameBuffer, int bufferSize);

    SSAPPNATIVE_API bool ConnectPlc(const char* ipAddress, int port);

    SSAPPNATIVE_API void DisconnectPlc();

    SSAPPNATIVE_API int GetLastPlcValue(); // Returns value of D0

    SSAPPNATIVE_API bool GetIsConnected(); // Returns true if connected

    SSAPPNATIVE_API bool GetIsCameraConnected(); // Returns true if camera is connected

    // Camera Exposure Controls
    SSAPPNATIVE_API int SetCameraExposureAuto(int mode); // 0=Off, 1=Once, 2=Continuous
    SSAPPNATIVE_API int SetCameraExposureTime(float exposureTimeUs); // Exposure time in microseconds
    SSAPPNATIVE_API int GetCameraExposureAuto(); // Get current auto mode
    SSAPPNATIVE_API float GetCameraExposureTime(); // Get current time

    // New Control Functions
    SSAPPNATIVE_API void SetPlcBit(const char* device, int value);
    SSAPPNATIVE_API bool CaptureImageCustom(const char* filename);
}
