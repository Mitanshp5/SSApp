#include "framework.h"
#include "PlcControl.h"
#include "mcProtocol.h"
#include <thread>
#include <chrono>
#include <string>
#include <vector>
#include <iostream>
#include <atomic>
#include <mutex>
#include <memory>

// Global state for persistent connection
std::unique_ptr<MCProtocol> g_Plc;
std::atomic<bool> g_IsConnected(false);
std::atomic<int> g_LastD0Value(0); // Store the last read value
std::mutex g_PlcMutex;

void PollingThreadFunc() {
    // Polling loop: runs every 0.5s
    while (g_IsConnected) {
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
                    std::cerr << "Polling error: " << ex.what() << std::endl;
                    g_IsConnected = false; 
                    try { g_Plc->disconnect(); } catch (...) {}
                }
            }
            else {
                 g_IsConnected = false;
            }
        }
        
        if (!g_IsConnected) break;
        std::this_thread::sleep_for(std::chrono::milliseconds(500));
    }
}

// ---------------------------------------------------------
// EXPORTED FUNCTIONS
// ---------------------------------------------------------

void DisconnectPlc() {
    g_IsConnected = false;
    std::lock_guard<std::mutex> lock(g_PlcMutex);
    if (g_Plc) {
        g_Plc->disconnect();
    }
}

bool ConnectPlc(const char* ipAddress, int port) {
    // Cleanup existing
    DisconnectPlc();

    std::lock_guard<std::mutex> lock(g_PlcMutex);
    
    // Create new instance
    g_Plc = std::make_unique<MCProtocol>();

    try {
        if (g_Plc->connect(ipAddress, port)) {
            g_IsConnected = true;
            // Start polling thread
            std::thread t(PollingThreadFunc);
            t.detach();
            return true;
        }
    }
    catch (const std::exception& ex) {
        std::cerr << "Connection failed: " << ex.what() << std::endl;
    }

    g_IsConnected = false;
    return false;
}

void StartScanNative(const char* /*ipAddress*/, int /*port*/) {
    std::thread t([]() {
        if (g_IsConnected) {
             // 1. Turn Y1 ON
             {
                 std::lock_guard<std::mutex> lock(g_PlcMutex);
                 if (g_Plc && g_Plc->isConnected()) {
                     try {
                        g_Plc->write_bit("Y1", { 1 });
                     } catch (...) {}
                 }
             }
             
             std::this_thread::sleep_for(std::chrono::seconds(5));
             
             // 2. Turn Y1 OFF
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
