#include "framework.h"
#include "PlcControl.h"
#include "mcProtocol.h"
#include "MvCameraControl.h"
#include <thread>
#include <chrono>
#include <string>
#include <vector>
#include <iostream>
#include <fstream>
#include <atomic>
#include <mutex>
#include <memory>
#include <direct.h> // For _mkdir
#include <cstring>
#include <cstdlib> // For _TRUNCATE

// Global state for persistent connection
std::unique_ptr<MCProtocol> g_Plc;
std::atomic<bool> g_IsConnected(false);
std::atomic<int> g_LastD0Value(0); // Store the last read value
std::mutex g_PlcMutex;

// Reconnection Logic Globals
std::string g_TargetIP;
int g_TargetPort = 0;
std::atomic<bool> g_ShouldReconnect(false);
std::atomic<bool> g_ThreadRunning(false);

// Camera Globals
void* g_CamHandle = nullptr;
std::atomic<bool> g_CamLiveViewRunning(false);
std::thread g_CamThread;
std::mutex g_CamMutex;
std::atomic<bool> g_CaptureRequest(false);
void* g_LiveViewHwnd = nullptr;
MV_CC_DEVICE_INFO_LIST g_DeviceList = {0}; // Cache device list

// Capture Synchronization
std::mutex g_CaptureMutex;
std::condition_variable g_CaptureCV;
bool g_CaptureFinished = false;
std::string g_CaptureFilename;

void LogNative(const std::string& msg) {
    try {
        std::ofstream outfile("native_debug.log", std::ios_base::app);
        auto now = std::chrono::system_clock::now();
        std::time_t now_c = std::chrono::system_clock::to_time_t(now);
        outfile << std::put_time(std::localtime(&now_c), "%F %T") << " - " << msg << std::endl;
    } catch (...) {}
}

// Camera Helper
void EnsureImagesFolder() {
    _mkdir("images");
}

void SaveImageFromBuffer(unsigned char* pData, unsigned int dataSize, MV_FRAME_OUT_INFO_EX* pFrameInfo, const std::string& customName = "") {
    if (!pData || !pFrameInfo) return;

    EnsureImagesFolder();
    
    std::string filename;
    if (!customName.empty()) {
        filename = "images/" + customName;
    } else {
        // Generate filename based on timestamp
        auto now = std::chrono::system_clock::now();
        auto timestamp = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()).count();
        filename = "images/img_" + std::to_string(timestamp) + ".bmp";
    }

    MV_SAVE_IMAGE_TO_FILE_PARAM_EX stSaveParam;
    memset(&stSaveParam, 0, sizeof(MV_SAVE_IMAGE_TO_FILE_PARAM_EX));
    stSaveParam.enPixelType = pFrameInfo->enPixelType;
    stSaveParam.nWidth = pFrameInfo->nWidth;
    stSaveParam.nHeight = pFrameInfo->nHeight;
    stSaveParam.pData = pData;
    stSaveParam.nDataLen = pFrameInfo->nFrameLen;
    stSaveParam.enImageType = MV_Image_Bmp;
    stSaveParam.pcImagePath = const_cast<char*>(filename.c_str());

    int nRet = MV_CC_SaveImageToFileEx(g_CamHandle, &stSaveParam);
    if (nRet != MV_OK) {
        LogNative("Failed to save image: " + std::to_string(nRet));
    } else {
        LogNative("Image saved: " + filename);
    }
}

void CameraLoop() {
    LogNative("Camera Thread Started");
    
    MV_FRAME_OUT_INFO_EX stImageInfo = {0};
    unsigned char* pData = (unsigned char*)malloc(sizeof(unsigned char) * (1920 * 1200 * 3 + 2048)); // Alloc buffer (adjust size as needed, using safe large default)
    if (!pData) return;

    while (g_CamLiveViewRunning) {
        if (!g_CamHandle) {
             std::this_thread::sleep_for(std::chrono::milliseconds(100));
             continue;
        }

        int nRet = MV_CC_GetOneFrameTimeout(g_CamHandle, pData, 1920 * 1200 * 3 + 2048, &stImageInfo, 1000);
        if (nRet == MV_OK) {
            // 1. Display
            if (g_LiveViewHwnd) {
                MV_DISPLAY_FRAME_INFO stDisplayInfo = {0};
                stDisplayInfo.hWnd = g_LiveViewHwnd;
                stDisplayInfo.pData = pData;
                stDisplayInfo.nDataLen = stImageInfo.nFrameLen;
                stDisplayInfo.nWidth = stImageInfo.nWidth;
                stDisplayInfo.nHeight = stImageInfo.nHeight;
                stDisplayInfo.enPixelType = stImageInfo.enPixelType;
                
                MV_CC_DisplayOneFrame(g_CamHandle, &stDisplayInfo);
            }

            // 2. Capture if requested
            if (g_CaptureRequest) {
                std::string fname;
                {
                     std::lock_guard<std::mutex> lk(g_CaptureMutex);
                     fname = g_CaptureFilename;
                }
                SaveImageFromBuffer(pData, stImageInfo.nFrameLen, &stImageInfo, fname);
                
                {
                    std::lock_guard<std::mutex> lk(g_CaptureMutex);
                    g_CaptureFinished = true;
                }
                g_CaptureCV.notify_all();
                g_CaptureRequest = false;
            }
        }
    }

    free(pData);
    LogNative("Camera Thread Stopped");
}

void ConnectionManager() {
    LogNative("ConnectionManager Thread Started");
    g_ThreadRunning = true;
    while (true) {
        bool shouldRun = g_ShouldReconnect;
        
        // 1. If we shouldn't be connected, ensure we are disconnected and wait
        if (!shouldRun) {
            bool wasConnected = false;
            {
                std::lock_guard<std::mutex> lock(g_PlcMutex);
                if (g_Plc && g_Plc->isConnected()) {
                    g_Plc->disconnect();
                    wasConnected = true;
                }
            }
            if (wasConnected) {
                g_IsConnected = false;
                LogNative("Manager: Forced Disconnect (shouldRun=false)");
            }
            
            std::this_thread::sleep_for(std::chrono::milliseconds(200));
            continue;
        }

        // 2. Check Connection State
        bool currentConnected = false;
        {
            std::lock_guard<std::mutex> lock(g_PlcMutex);
            if (g_Plc) currentConnected = g_Plc->isConnected();
        }
        g_IsConnected = currentConnected;

        if (currentConnected) {
            // --- POLLING PHASE ---
            {
                std::lock_guard<std::mutex> lock(g_PlcMutex);
                if (g_Plc && g_Plc->isConnected()) {
                    try {
                        // Read D0 (1 word)
                        auto result = g_Plc->read_sign_word("D0", 1); 
                        if (!result.empty()) {
                            g_LastD0Value = result[0];
                        }
                    }
                    catch (const std::exception& ex) {
                        LogNative(std::string("Polling error: ") + ex.what());
                        g_IsConnected = false; 
                        try { g_Plc->disconnect(); } catch (...) {}
                    }
                }
                else {
                     g_IsConnected = false;
                }
            }
            // Poll interval
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
        } 
        else {
            // --- RECONNECTION PHASE ---
            std::string ip;
            int port;
            {
                std::lock_guard<std::mutex> lock(g_PlcMutex);
                ip = g_TargetIP;
                port = g_TargetPort;
            }

            if (!ip.empty() && port > 0) {
                // Try to connect
                bool success = false;
                LogNative("Manager: Attempting to connect to " + ip + ":" + std::to_string(port));
                
                {
                    std::lock_guard<std::mutex> lock(g_PlcMutex);
                    if (!g_Plc) g_Plc = std::make_unique<MCProtocol>();
                    
                    try {
                        if (g_Plc->connect(ip, port)) {
                            success = true;
                        }
                    }
                    catch (const std::exception& ex) {
                        LogNative(std::string("Connection exception: ") + ex.what());
                    }
                }

                if (success) {
                    g_IsConnected = true;
                    LogNative("Manager: Connected successfully.");
                    // Proceed immediately to polling next loop
                } else {
                    LogNative("Manager: Connection failed. Retrying in 5s...");
                    // Retry Delay (5 Seconds)
                    std::this_thread::sleep_for(std::chrono::seconds(5));
                }
            } else {
                // Missing config
                std::this_thread::sleep_for(std::chrono::milliseconds(500));
            }
        }
    }
}

void EnsureThreadStarted() {
    static std::once_flag flag;
    std::call_once(flag, []() {
        std::thread t(ConnectionManager);
        t.detach();
    });
}


// ---------------------------------------------------------
// EXPORTED FUNCTIONS
// ---------------------------------------------------------
bool ConnectPlc(const char* ipAddress, int port) {
    LogNative("ConnectPlc called: " + std::string(ipAddress) + ":" + std::to_string(port));
    
    // Update target
    {
        std::lock_guard<std::mutex> lock(g_PlcMutex);
        g_TargetIP = ipAddress;
        g_TargetPort = port;
    }
    
    // Signal manager
    g_ShouldReconnect = true;
    
    // Ensure thread is running
    EnsureThreadStarted();
    
    return true; 
}

void DisconnectPlc() {
    LogNative("DisconnectPlc called");
    g_ShouldReconnect = false;
}

int GetCameraCount() {
    memset(&g_DeviceList, 0, sizeof(MV_CC_DEVICE_INFO_LIST));
    int nRet = MV_CC_EnumDevices(MV_GIGE_DEVICE | MV_USB_DEVICE, &g_DeviceList);
    if (MV_OK != nRet) {
        LogNative("GetCameraCount: EnumDevices failed");
        return 0;
    }
    return (int)g_DeviceList.nDeviceNum;
}

bool GetCameraName(int index, char* nameBuffer, int bufferSize) {
    if (index < 0 || index >= (int)g_DeviceList.nDeviceNum || !nameBuffer) return false;
    
    MV_CC_DEVICE_INFO* pDeviceInfo = g_DeviceList.pDeviceInfo[index];
    if (!pDeviceInfo) return false;

    if (pDeviceInfo->nTLayerType == MV_GIGE_DEVICE) {
        // UserDefinedName is usually preferred if set, otherwise ModelName
        if (strlen((char*)pDeviceInfo->SpecialInfo.stGigEInfo.chUserDefinedName) > 0) {
            strncpy_s(nameBuffer, bufferSize, (char*)pDeviceInfo->SpecialInfo.stGigEInfo.chUserDefinedName, _TRUNCATE);
        } else {
             strncpy_s(nameBuffer, bufferSize, (char*)pDeviceInfo->SpecialInfo.stGigEInfo.chModelName, _TRUNCATE);
        }
    } 
    else if (pDeviceInfo->nTLayerType == MV_USB_DEVICE) {
        if (strlen((char*)pDeviceInfo->SpecialInfo.stUsb3VInfo.chUserDefinedName) > 0) {
             strncpy_s(nameBuffer, bufferSize, (char*)pDeviceInfo->SpecialInfo.stUsb3VInfo.chUserDefinedName, _TRUNCATE);
        } else {
             strncpy_s(nameBuffer, bufferSize, (char*)pDeviceInfo->SpecialInfo.stUsb3VInfo.chModelName, _TRUNCATE);
        }
    }
    else {
        strncpy_s(nameBuffer, bufferSize, "Unknown Device", _TRUNCATE);
    }
    return true;
}

void StartLiveView(void* hWnd, int deviceIndex) {
    LogNative("StartLiveView called with index " + std::to_string(deviceIndex));
    std::lock_guard<std::mutex> lock(g_CamMutex);
    
    if (g_CamLiveViewRunning) {
        g_LiveViewHwnd = hWnd; 
        return; 
    }

    // Ensure list is populated if index is provided without prior Enum
    if (g_DeviceList.nDeviceNum == 0) {
        GetCameraCount(); 
    }

    if (deviceIndex < 0 || deviceIndex >= (int)g_DeviceList.nDeviceNum) {
        LogNative("Invalid device index");
        return;
    }

    // 2. Create Handle
    int nRet = MV_CC_CreateHandle(&g_CamHandle, g_DeviceList.pDeviceInfo[deviceIndex]);
    if (MV_OK != nRet) {
        LogNative("CreateHandle failed: " + std::to_string(nRet));
        return;
    }

    // 3. Open Device
    nRet = MV_CC_OpenDevice(g_CamHandle);
    if (MV_OK != nRet) {
        LogNative("OpenDevice failed: " + std::to_string(nRet));
        MV_CC_DestroyHandle(g_CamHandle);
        g_CamHandle = nullptr;
        return;
    }

    // 4. Start Grabbing
    nRet = MV_CC_StartGrabbing(g_CamHandle);
    if (MV_OK != nRet) {
        LogNative("StartGrabbing failed: " + std::to_string(nRet));
        MV_CC_CloseDevice(g_CamHandle);
        MV_CC_DestroyHandle(g_CamHandle);
        g_CamHandle = nullptr;
        return;
    }

    g_LiveViewHwnd = hWnd;
    g_CamLiveViewRunning = true;
    g_CamThread = std::thread(CameraLoop);
    g_CamThread.detach();
}

void StopLiveView() {
    LogNative("StopLiveView called");
    g_CamLiveViewRunning = false;
    
    // Wait slightly for thread to exit loop
    std::this_thread::sleep_for(std::chrono::milliseconds(200)); 
    
    std::lock_guard<std::mutex> lock(g_CamMutex);
    if (g_CamHandle) {
        MV_CC_StopGrabbing(g_CamHandle);
        MV_CC_CloseDevice(g_CamHandle);
        MV_CC_DestroyHandle(g_CamHandle);
        g_CamHandle = nullptr;
    }
    LogNative("StopLiveView finished");
}



void StartScanNative(const char* /*ipAddress*/, int /*port*/) {
    std::thread t([]() {
        if (g_IsConnected) {
             {
                 std::lock_guard<std::mutex> lock(g_PlcMutex);
                 if (g_Plc && g_Plc->isConnected()) {
                     try {
                        g_Plc->write_bit("Y1", { 1 });
                     } catch (...) {}
                 }
             }
             std::this_thread::sleep_for(std::chrono::seconds(5));
             {
                 std::lock_guard<std::mutex> lock(g_PlcMutex);
                 if (g_Plc && g_Plc->isConnected()) {
                     try {
                        g_Plc->write_bit("Y1", { 0 });
                     } catch (...) {}
                 }
             }
        }
    });
    t.detach();
}

int GetLastPlcValue() {
    return g_LastD0Value;
}

bool GetIsConnected() {
    return g_IsConnected;
}

bool GetIsCameraConnected() {
    return g_CamHandle != nullptr;
}

// ---------------------------------------------------------
// CAMERA EXPOSURE SETTINGS
// ---------------------------------------------------------

int SetCameraExposureAuto(int mode) {
    std::lock_guard<std::mutex> lock(g_CamMutex);
    if (!g_CamHandle) return -1; // Not initialized

    // "ExposureAuto" : 0=Off, 1=Once, 2=Continuous
    // Note: Enum values might differ by camera, but standard GenICam usually maps:
    // Off = 0, Once = 1, Continuous = 2.
    // MVS SDK uses integer values for Enums.
    
    int nRet = MV_CC_SetEnumValue(g_CamHandle, "ExposureAuto", (unsigned int)mode);
    if (nRet != MV_OK) {
        LogNative("SetExposureAuto failed: " + std::to_string(nRet));
    }
    return nRet;
}

int SetCameraExposureTime(float exposureTimeUs) {
    std::lock_guard<std::mutex> lock(g_CamMutex);
    if (!g_CamHandle) return -1;

    // "ExposureTime"
    int nRet = MV_CC_SetFloatValue(g_CamHandle, "ExposureTime", exposureTimeUs);
    if (nRet != MV_OK) {
        LogNative("SetExposureTime failed: " + std::to_string(nRet));
    }
    return nRet;
}

int GetCameraExposureAuto() {
    std::lock_guard<std::mutex> lock(g_CamMutex);
    if (!g_CamHandle) return -1;

    MVCC_ENUMVALUE stEnumValue = {0};
    int nRet = MV_CC_GetEnumValue(g_CamHandle, "ExposureAuto", &stEnumValue);
    if (nRet != MV_OK) {
        LogNative("GetExposureAuto failed: " + std::to_string(nRet));
        return -1;
    }
    return (int)stEnumValue.nCurValue;
}

float GetCameraExposureTime() {
    std::lock_guard<std::mutex> lock(g_CamMutex);
    if (!g_CamHandle) return -1.0f;

    MVCC_FLOATVALUE stFloatValue = {0};
    int nRet = MV_CC_GetFloatValue(g_CamHandle, "ExposureTime", &stFloatValue);
    if (nRet != MV_OK) {
        LogNative("GetExposureTime failed: " + std::to_string(nRet));
        return -1.0f;
    }
    return stFloatValue.fCurValue;
}

void SetPlcBit(const char* device, int value) {
    if (g_IsConnected) {
        std::lock_guard<std::mutex> lock(g_PlcMutex);
        if (g_Plc && g_Plc->isConnected()) {
            try {
                g_Plc->write_bit(device, { value });
            } catch (...) {}
        }
    }
}

bool CaptureImageCustom(const char* filename) {
    if (!g_CamLiveViewRunning) return false;

    {
        std::lock_guard<std::mutex> lock(g_CaptureMutex);
        g_CaptureFilename = filename;
        g_CaptureFinished = false;
        g_CaptureRequest = true;
    }

    // Wait for completion (timeout 5s)
    {
        std::unique_lock<std::mutex> lock(g_CaptureMutex);
        if (g_CaptureCV.wait_for(lock, std::chrono::seconds(5), []{ return g_CaptureFinished; })) {
             return true;
        }
    }
    return false; // Timeout
}

