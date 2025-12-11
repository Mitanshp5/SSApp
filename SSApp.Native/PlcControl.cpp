#include "framework.h"
#include "PlcControl.h"
#include "mcProtocol.h"
#include <thread>
#include <chrono>
#include <string>
#include <vector>
#include <iostream>
#include <fstream>
#include <atomic>
#include <mutex>
#include <memory>

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

void LogNative(const std::string& msg) {
    try {
        std::ofstream outfile("native_debug.log", std::ios_base::app);
        auto now = std::chrono::system_clock::now();
        std::time_t now_c = std::chrono::system_clock::to_time_t(now);
        outfile << std::put_time(std::localtime(&now_c), "%F %T") << " - " << msg << std::endl;
    } catch (...) {}
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

void DisconnectPlc() {
    LogNative("Exported: DisconnectPlc called");
    g_ShouldReconnect = false;
    // Immediate disconnect handling is done in the loop, 
    // but we can force it here for responsiveness
    std::lock_guard<std::mutex> lock(g_PlcMutex);
    if (g_Plc) {
        g_Plc->disconnect();
    }
    g_IsConnected = false;
}

bool ConnectPlc(const char* ipAddress, int port) {
    if (ipAddress) {
        LogNative(std::string("Exported: ConnectPlc called for ") + ipAddress + ":" + std::to_string(port));
    } else {
        LogNative("Exported: ConnectPlc called with NULL IP");
    }

    // 1. Update Configuration
    {
        std::lock_guard<std::mutex> lock(g_PlcMutex);
        g_TargetIP = ipAddress ? ipAddress : "";
        g_TargetPort = port;
        
        // If we are switching targets, force disconnect first to be clean
        if (g_Plc && g_Plc->isConnected()) {
            g_Plc->disconnect();
            g_IsConnected = false;
        }
    }

    // 2. Enable Reconnection Logic
    g_ShouldReconnect = true;

    // 3. Ensure Manager Thread is Running
    EnsureThreadStarted();

    // Always return true to indicate "Request Accepted"
    // The actual connection happens asynchronously.
    return true; 
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
