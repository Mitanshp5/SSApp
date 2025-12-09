#include "framework.h"
#include "PlcControl.h"
#include <thread>
#include <chrono>
#include <string>
#include <vector>
#include <iostream>

// Link with Winsock library
#pragma comment(lib, "ws2_32.lib")

// Helper to build MC Protocol 3E Binary Write Packet
std::vector<unsigned char> BuildMcWriteBitPacket(int address, bool turnOn) {
    // 3E Frame, Binary Mode
    std::vector<unsigned char> packet;

    // --- Header ---
    packet.push_back(0x50); packet.push_back(0x00); // Subheader (Request)
    packet.push_back(0x00);                         // Network No
    packet.push_back(0xFF);                         // PC No
    packet.push_back(0xFF); packet.push_back(0x03); // Request Destination Module I/O No
    packet.push_back(0x00);                         // Request Destination Station No

    // --- Request Data ---
    // We need to calculate length later, for now placeholder
    // Length covers from Timer to Data
    // Timer(2) + Cmd(2) + Sub(2) + Dev(3) + Code(1) + Count(2) + Data(1) = 13 bytes
    packet.push_back(0x0D); packet.push_back(0x00); // Data Length (13 bytes)

    packet.push_back(0x10); packet.push_back(0x00); // CPU Monitoring Timer (4s approx)
    
    // Command: 1401 (Batch Write) -> 01 14 (Little Endian)
    packet.push_back(0x01); packet.push_back(0x14);
    
    // Subcommand: 0001 (Bit Unit) -> 01 00
    packet.push_back(0x01); packet.push_back(0x00);
    
    // Device Number (3 bytes) - Little Endian
    // Y1 -> Address 1
    packet.push_back((unsigned char)(address & 0xFF));
    packet.push_back((unsigned char)((address >> 8) & 0xFF));
    packet.push_back((unsigned char)((address >> 16) & 0xFF));
    
    // Device Code: Y (0x9D)
    packet.push_back(0x9D);
    
    // Number of device points (1 point)
    packet.push_back(0x01); packet.push_back(0x00);
    
    // Data (1 point = 1 nibble -> 1 byte in 4-bit mode?)
    // In Subcommand 0001, 1 bit ON = 0x10, OFF = 0x00 (High nibble of first byte)
    packet.push_back(turnOn ? 0x10 : 0x00);

    return packet;
}

void PlcThreadFunc(std::string ip, int port) {
    // Initialize Winsock
    WSADATA wsaData;
    int iResult = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (iResult != 0) {
        return;
    }

    SOCKET ConnectSocket = INVALID_SOCKET;
    struct addrinfo* result = NULL, * ptr = NULL, hints;

    ZeroMemory(&hints, sizeof(hints));
    hints.ai_family = AF_UNSPEC;
    hints.ai_socktype = SOCK_STREAM;
    hints.ai_protocol = IPPROTO_TCP;

    // Resolve the server address and port
    iResult = getaddrinfo(ip.c_str(), std::to_string(port).c_str(), &hints, &result);
    if (iResult != 0) {
        WSACleanup();
        return;
    }

    // Attempt to connect to an address until one succeeds
    for (ptr = result; ptr != NULL; ptr = ptr->ai_next) {
        ConnectSocket = socket(ptr->ai_family, ptr->ai_socktype, ptr->ai_protocol);
        if (ConnectSocket == INVALID_SOCKET) {
            WSACleanup();
            return;
        }

        iResult = connect(ConnectSocket, ptr->ai_addr, (int)ptr->ai_addrlen);
        if (iResult == SOCKET_ERROR) {
            closesocket(ConnectSocket);
            ConnectSocket = INVALID_SOCKET;
            continue;
        }
        break;
    }

    freeaddrinfo(result);

    if (ConnectSocket == INVALID_SOCKET) {
        WSACleanup();
        return;
    }

    // --- SEND MC PROTOCOL COMMANDS ---
    
    // 1. Turn Y1 ON
    std::vector<unsigned char> turnOn = BuildMcWriteBitPacket(1, true);
    send(ConnectSocket, (const char*)turnOn.data(), (int)turnOn.size(), 0);

    // (Optional) Read response here if needed, but we'll fire-and-forget for this task
    // MC Protocol returns a response header + 0000 (End Code) on success.

    // 2. Wait 5 seconds
    std::this_thread::sleep_for(std::chrono::seconds(5));

    // 3. Turn Y1 OFF
    std::vector<unsigned char> turnOff = BuildMcWriteBitPacket(1, false);
    send(ConnectSocket, (const char*)turnOff.data(), (int)turnOff.size(), 0);

    // Cleanup
    shutdown(ConnectSocket, SD_SEND);
    closesocket(ConnectSocket);
    WSACleanup();
}

void StartScanNative(const char* ipAddress, int port) {
    std::string ip(ipAddress);
    // Launch in a detached thread
    std::thread t(PlcThreadFunc, ip, port);
    t.detach();
}
