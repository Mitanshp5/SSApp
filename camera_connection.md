# Camera Connection & Control Guide (MvCameraControl.Net)

This document provides the necessary code snippets and explanations to integrate Hikrobot/MVS camera features into a C# .NET application.

## 1. Prerequisites & Initialization

**References:**
Ensure `MvCameraControl.Net.dll` is referenced in your project.
Use namespace:
```csharp
using MvCamCtrl.NET;
using System.Runtime.InteropServices; // For Marshal operations
```

**SDK Initialization:**
Must be called before any other SDK operations.
```csharp
MyCamera.MV_CC_Initialize_NET();
```

## 2. Device Enumeration & Connection

**Enum Devices:**
Finds available GigE, USB3, etc. cameras.
```csharp
MyCamera.MV_CC_DEVICE_INFO_LIST stDeviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
int nRet = MyCamera.MV_CC_EnumDevices_NET(
    MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, 
    ref stDeviceList
);

if (nRet != 0) { /* Handle Error */ }

// Iterate devices
for (int i = 0; i < stDeviceList.nDeviceNum; i++)
{
    MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(
        stDeviceList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO)
    );
    // Identify by device.SpecialInfo.stGigEInfo or stUsb3VInfo
}
```

**Create & Open Device:**
```csharp
MyCamera m_MyCamera = new MyCamera();

// 'device' comes from the enumeration step above
nRet = m_MyCamera.MV_CC_CreateDevice_NET(ref device);
nRet = m_MyCamera.MV_CC_OpenDevice_NET();

// For GigE Cameras: Optimize Packet Size
if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
{
    int nPacketSize = m_MyCamera.MV_CC_GetOptimalPacketSize_NET();
    if (nPacketSize > 0)
    {
        m_MyCamera.MV_CC_SetIntValueEx_NET("GevSCPSPacketSize", nPacketSize);
    }
}
```

## 3. Basic Configuration

**Set Trigger Mode (Off for Continuous):**
```csharp
// Turn off trigger mode for continuous grabbing
m_MyCamera.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
```

**Start Grabbing:**
```csharp
nRet = m_MyCamera.MV_CC_StartGrabbing_NET();
```

## 4. Live Display & Image Acquisition

**Grabbing Loop (Thread):**
```csharp
bool m_bGrabbing = true;
MyCamera.MV_FRAME_OUT stFrameInfo = new MyCamera.MV_FRAME_OUT();
MyCamera.MV_DISPLAY_FRAME_INFO stDisplayInfo = new MyCamera.MV_DISPLAY_FRAME_INFO();

while (m_bGrabbing)
{
    // Wait up to 1000ms for a frame
    int nRet = m_MyCamera.MV_CC_GetImageBuffer_NET(ref stFrameInfo, 1000);
    
    if (nRet == MyCamera.MV_OK)
    {
        // 1. Direct Display to a Handle (e.g., PictureBox)
        stDisplayInfo.hWnd = pictureBox1.Handle; 
        stDisplayInfo.pData = stFrameInfo.pBufAddr;
        stDisplayInfo.nDataLen = stFrameInfo.stFrameInfo.nFrameLen;
        stDisplayInfo.nWidth = stFrameInfo.stFrameInfo.nWidth;
        stDisplayInfo.nHeight = stFrameInfo.stFrameInfo.nHeight;
        stDisplayInfo.enPixelType = stFrameInfo.stFrameInfo.enPixelType;
        
        m_MyCamera.MV_CC_DisplayOneFrame_NET(ref stDisplayInfo);

        // 2. Free the buffer (Crucial!)
        m_MyCamera.MV_CC_FreeImageBuffer_NET(ref stFrameInfo);
    }
}
```

## 5. Parameter Settings (GenICam Nodes)

Use these methods to adjust camera settings dynamically.

**Exposure Time (Float):**
```csharp
// Turn off Auto Exposure first if necessary
m_MyCamera.MV_CC_SetEnumValue_NET("ExposureAuto", 0); // 0: Off, 1: Continuous

// Set Value (microseconds)
float exposureTime = 5000.0f;
int nRet = m_MyCamera.MV_CC_SetFloatValue_NET("ExposureTime", exposureTime);

// Get Value
MyCamera.MVCC_FLOATVALUE stParam = new MyCamera.MVCC_FLOATVALUE();
m_MyCamera.MV_CC_GetFloatValue_NET("ExposureTime", ref stParam);
float currentExposure = stParam.fCurValue;
```

**Gain (Float):**
```csharp
m_MyCamera.MV_CC_SetEnumValue_NET("GainAuto", 0); // 0: Off, 1: Continuous
m_MyCamera.MV_CC_SetFloatValue_NET("Gain", 10.0f); // dB
```

**Frame Rate (Float):**
```csharp
m_MyCamera.MV_CC_SetBoolValue_NET("AcquisitionFrameRateEnable", true);
m_MyCamera.MV_CC_SetFloatValue_NET("AcquisitionFrameRate", 30.0f);
```

**Gamma (Float / Bool):**
*Note: Node names may vary by camera model (e.g., "Gamma", "IspGamma").*
```csharp
// Enable Gamma
m_MyCamera.MV_CC_SetBoolValue_NET("GammaEnable", true); 
// Set Gamma Value
m_MyCamera.MV_CC_SetFloatValue_NET("Gamma", 0.7f);
```

**Contrast:**
Contrast might be available via `MV_CC_ImageContrast_NET` (software processing) or specific nodes.
```csharp
// Example using the specific API for buffer processing
MyCamera.MV_CC_CONTRAST_PARAM stContrastParam = new MyCamera.MV_CC_CONTRAST_PARAM();
stContrastParam.nContrastFactor = 50; // Example factor
// This function usually operates on the handle/internal state or a buffer
m_MyCamera.MV_CC_ImageContrast_NET(ref stContrastParam);
```

## 6. Image Capture (Saving)

To save the current frame as an image file (BMP/JPG/PNG).

```csharp
// Assumes you have the frame buffer 'pData' from MV_CC_GetImageBuffer_NET
MyCamera.MV_SAVE_IMG_TO_FILE_PARAM stSaveFileParam = new MyCamera.MV_SAVE_IMG_TO_FILE_PARAM();

stSaveFileParam.enImageType = MyCamera.MV_SAVE_IAMGE_TYPE.MV_Image_Jpeg; // or MV_Image_Bmp
stSaveFileParam.enPixelType = stFrameInfo.stFrameInfo.enPixelType;
stSaveFileParam.pData = stFrameInfo.pBufAddr;
stSaveFileParam.nDataLen = stFrameInfo.stFrameInfo.nFrameLen;
stSaveFileParam.nHeight = stFrameInfo.stFrameInfo.nHeight;
stSaveFileParam.nWidth = stFrameInfo.stFrameInfo.nWidth;
stSaveFileParam.nQuality = 80; // For JPEG
stSaveFileParam.iMethodValue = 2; // Method
stSaveFileParam.pImagePath = "CapturedImage.jpg";

int nRet = m_MyCamera.MV_CC_SaveImageToFile_NET(ref stSaveFileParam);
```

## 7. Disconnect & Cleanup

Crucial to release resources properly.

```csharp
// Stop Grabbing
m_MyCamera.MV_CC_StopGrabbing_NET();

// Close Device
m_MyCamera.MV_CC_CloseDevice_NET();
m_MyCamera.MV_CC_DestroyDevice_NET();

// Finalize SDK (Application Exit)
MyCamera.MV_CC_Finalize_NET();
```
