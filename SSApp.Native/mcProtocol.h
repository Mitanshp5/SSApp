#ifndef MCPROTOCOL_H
#define MCPROTOCOL_H

#define NOMINMAX

#include <iostream>
#include <string>
#include <vector>
#include <map>
#include <cstdint>
#include <algorithm>
#include <stdexcept>
#include <sstream>
#include <iomanip>
#include <cctype>

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "Ws2_32.lib")
#else
#include <sys/socket.h>
#include <arpa/inet.h>
#include <unistd.h>
#include <cstring>
#define INVALID_SOCKET -1
#define SOCKET_ERROR -1
#define SOCKET int
#define closesocket close
#endif

class MCProtocol {
public:
    enum DeviceType {
        M, L, F, D, R, B, W, X, Y
    };

    MCProtocol() : sock(INVALID_SOCKET), is_connected(false) {
#ifdef _WIN32
        WSADATA wsaData;
        if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
            throw std::runtime_error("WSAStartup failed");
        }
#endif
        initializeTables();
    }

    ~MCProtocol() {
        disconnect();
#ifdef _WIN32
        WSACleanup();
#endif
    }

    bool connect(const std::string& host, int port) {
        // ensure clean
        if (sock != INVALID_SOCKET) {
            disconnect();
        }

        sock = socket(AF_INET, SOCK_STREAM, 0);
        if (sock == INVALID_SOCKET) {
            return false;
        }

        sockaddr_in serverAddr{};
        serverAddr.sin_family = AF_INET;
        serverAddr.sin_port = htons(port);

        if (inet_pton(AF_INET, host.c_str(), &serverAddr.sin_addr) <= 0) {
            closesocket(sock);
            sock = INVALID_SOCKET;
            return false;
        }

        if (::connect(sock, (struct sockaddr*)&serverAddr, sizeof(serverAddr)) < 0) {
            closesocket(sock);
            sock = INVALID_SOCKET;
            return false;
        }

        // Set timeout to 6 seconds (same as Python)
#ifdef _WIN32
        DWORD timeout = 6000;
        setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, (const char*)&timeout, sizeof(timeout));
        setsockopt(sock, SOL_SOCKET, SO_SNDTIMEO, (const char*)&timeout, sizeof(timeout));
#else
        struct timeval tv {};
        tv.tv_sec = 6;
        tv.tv_usec = 0;
        setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, (const char*)&tv, sizeof(tv));
        setsockopt(sock, SOL_SOCKET, SO_SNDTIMEO, (const char*)&tv, sizeof(tv));
#endif

        is_connected = true;
        return true;
    }

    void disconnect() {
        if (sock != INVALID_SOCKET) {
            closesocket(sock);
            sock = INVALID_SOCKET;
        }
        is_connected = false;
    }

    bool isConnected() const {
        return is_connected;
    }

    // Read Word (Signed 16-bit)
    std::vector<int16_t> read_sign_word(const std::string& headdevice, int length) {
        checkConnected();
        validateRequest(headdevice, "read_sign_word", length);
        std::vector<uint8_t> response = sendRequest(headdevice, length, "read_word");
        return parseWordResponse<int16_t>(response, length);
    }

    // Read DWord (Signed 32-bit)
    std::vector<int32_t> read_sign_dword(const std::string& headdevice, int length) {
        checkConnected();
        validateRequest(headdevice, "read_sign_Dword", length);
        // Python uses read_word frame with length*2 points
        std::vector<uint8_t> response = sendRequest(headdevice, length * 2, "read_word");
        return parseDWordResponse<int32_t>(response, length);
    }

    // Read Bit (Returns 0 or 1)
    std::vector<int> read_bit(const std::string& headdevice, int length) {
        checkConnected();
        validateRequest(headdevice, "read_bit", length);
        std::vector<uint8_t> response = sendRequest(headdevice, length, "read_bit");
        return parseBitResponse(response, length);
    }

    // Write Bit
    bool write_bit(const std::string& headdevice, const std::vector<int>& data) {
        checkConnected();
        int length = static_cast<int>(data.size());
        validateRequest(headdevice, "write_bit", length);

        if (length <= 0) {
            throw std::invalid_argument("write_bit length must be > 0");
        }

        // Replicate Python logic WITHOUT big-int limit:
        // if len is odd -> add one '0' nibble at the end
        std::vector<int> hexDigits;
        hexDigits.reserve(length + 1);
        for (int v : data) {
            hexDigits.push_back(v ? 1 : 0);
        }
        if (length % 2 != 0) {
            hexDigits.push_back(0);
        }

        int byteLength = static_cast<int>(hexDigits.size()) / 2;
        std::vector<uint8_t> byteData;
        byteData.reserve(byteLength);

        // Left-to-right mapping of hex digits to bytes (big-endian style)
        // Each pair of digits -> one byte: high nibble, low nibble
        for (int i = 0; i < byteLength; ++i) {
            int hi = hexDigits[2 * i];
            int lo = hexDigits[2 * i + 1];
            uint8_t b = static_cast<uint8_t>(((hi & 0x0F) << 4) | (lo & 0x0F));
            byteData.push_back(b);
        }

        return sendWriteRequest(headdevice, length, "write_bit", byteData);
    }

    // Write Word
    bool write_sign_word(const std::string& headdevice, const std::vector<int16_t>& data) {
        checkConnected();
        int length = static_cast<int>(data.size());
        validateRequest(headdevice, "write_sign_word", length);

        std::vector<uint8_t> byteData;
        byteData.reserve(data.size() * 2);
        for (int16_t val : data) {
            byteData.push_back(static_cast<uint8_t>(val & 0xFF));
            byteData.push_back(static_cast<uint8_t>((val >> 8) & 0xFF));
        }
        return sendWriteRequest(headdevice, length, "write_word", byteData);
    }

    // Write DWord
    bool write_sign_dword(const std::string& headdevice, const std::vector<int32_t>& data) {
        checkConnected();
        int length = static_cast<int>(data.size());
        validateRequest(headdevice, "write_sign_Dword", length);

        std::vector<uint8_t> byteData;
        byteData.reserve(data.size() * 4);
        for (int32_t val : data) {
            byteData.push_back(static_cast<uint8_t>(val & 0xFF));
            byteData.push_back(static_cast<uint8_t>((val >> 8) & 0xFF));
            byteData.push_back(static_cast<uint8_t>((val >> 16) & 0xFF));
            byteData.push_back(static_cast<uint8_t>((val >> 24) & 0xFF));
        }
        // Length in frame is in WORDs, so *2
        return sendWriteRequest(headdevice, length * 2, "write_word", byteData);
    }

private:
    SOCKET sock;
    bool is_connected;

    struct DeviceInfo {
        uint8_t code;
        int base; // 8, 10, 16
    };

    struct IOLimit {
        int base;    // 8 / 10 / 16
        int maxAddr; // max device number
    };

    std::map<std::string, DeviceInfo> device_map;
    std::map<std::string, std::vector<uint8_t>> base_packets;
    std::map<char, IOLimit> io_table;
    std::map<std::string, int> length_limit;

    void checkConnected() const {
        if (!is_connected || sock == INVALID_SOCKET) {
            throw std::runtime_error("Not connected to PLC");
        }
    }

    void initializeTables() {
        // Mapping based on element_list in Python
        device_map["M"] = { 0x90, 10 };
        device_map["L"] = { 0x92, 10 };
        device_map["F"] = { 0x93, 10 };
        device_map["D"] = { 0xA8, 10 };
        device_map["R"] = { 0xAF, 10 };
        device_map["B"] = { 0xA0, 16 };
        device_map["W"] = { 0xB4, 16 };
        device_map["X"] = { 0x9C, 8 };
        device_map["Y"] = { 0x9D, 8 };

        // IO ranges (io_table in Python)
        io_table['X'] = { 8, 1024 };
        io_table['Y'] = { 8, 1024 };
        io_table['M'] = { 10, 32768 };
        io_table['L'] = { 10, 32768 };
        io_table['F'] = { 10, 32768 };
        io_table['B'] = { 16, 32768 };
        io_table['D'] = { 10, 8000 };
        io_table['W'] = { 16, 32768 };
        io_table['R'] = { 10, 32768 };

        // Length limits (length_limit in Python)
        length_limit["read_sign_word"] = 960;
        length_limit["read_sign_Dword"] = 480;
        length_limit["write_sign_word"] = 960;
        length_limit["write_sign_Dword"] = 480;
        length_limit["read_bit"] = 3584;
        length_limit["write_bit"] = 3584;

        // Main data byte templates (Header + Command)
        base_packets["read_word"] = { 0x50,0x00,0x00,0xFF,0xFF,0x03,0x00,0x0C,0x00,0x00,0x00,0x01,0x04,0x00,0x00 };
        base_packets["read_bit"] = { 0x50,0x00,0x00,0xFF,0xFF,0x03,0x00,0x0C,0x00,0x00,0x00,0x01,0x04,0x01,0x00 };
        base_packets["write_bit"] = { 0x50,0x00,0x00,0xFF,0xFF,0x03,0x00,0x0C,0x00,0x00,0x00,0x01,0x14,0x01,0x00 };
        base_packets["write_word"] = { 0x50,0x00,0x00,0xFF,0xFF,0x03,0x00,0x0C,0x00,0x00,0x00,0x01,0x14,0x00,0x00 };
    }

    void validateRequest(const std::string& headdevice,
        const std::string& funcName,
        int length) const
    {
        if (length <= 0) {
            throw std::invalid_argument("Length must be > 0");
        }

        if (headdevice.empty()) {
            throw std::invalid_argument("Headdevice cannot be empty");
        }

        char dev = static_cast<char>(std::toupper(static_cast<unsigned char>(headdevice[0])));
        auto ioIt = io_table.find(dev);
        if (ioIt == io_table.end()) {
            throw std::invalid_argument("Invalid device in headdevice: " + headdevice);
        }

        auto lenIt = length_limit.find(funcName);
        if (lenIt != length_limit.end()) {
            if (length > lenIt->second) {
                std::ostringstream oss;
                oss << "Length exceeds limit for " << funcName
                    << " (length=" << length
                    << ", limit=" << lenIt->second << ")";
                throw std::invalid_argument(oss.str());
            }
        }

        // Address check similar to check_user_data_format
        const IOLimit& lim = ioIt->second;

        if (headdevice.size() < 2) {
            throw std::invalid_argument("No address after device in headdevice: " + headdevice);
        }

        std::string addrStr = headdevice.substr(1); // full address for range check
        int addr = 0;
        try {
            addr = std::stoi(addrStr, nullptr, lim.base);
        }
        catch (...) {
            throw std::invalid_argument("Invalid address format in headdevice: " + headdevice);
        }

        if (addr < 0 || addr > lim.maxAddr) {
            std::ostringstream oss;
            oss << "Address out of range in headdevice: " << headdevice
                << " (parsed=" << addr << ", max=" << lim.maxAddr << ")";
            throw std::invalid_argument(oss.str());
        }
    }

    std::vector<uint8_t> sendRequest(const std::string& headdevice, int length, const std::string& type) {
        std::vector<uint8_t> packet = constructPacket(headdevice, length, type, {});
        if (send(sock, reinterpret_cast<const char*>(packet.data()),
            static_cast<int>(packet.size()), 0) < 0) {
            throw std::runtime_error("Send failed");
        }
        return receiveResponse(length, type);
    }

    bool sendWriteRequest(const std::string& headdevice, int length,
        const std::string& type,
        const std::vector<uint8_t>& data)
    {
        std::vector<uint8_t> packet = constructPacket(headdevice, length, type, data);
        if (send(sock, reinterpret_cast<const char*>(packet.data()),
            static_cast<int>(packet.size()), 0) < 0) {
            return false;
        }
        (void)receiveResponse(0, "write_response");
        return true;
    }

    std::vector<uint8_t> constructPacket(const std::string& headdevice,
        int length,
        const std::string& type,
        const std::vector<uint8_t>& writeData)
    {
        std::string devTypeStr = headdevice.substr(0, 1);
        std::transform(devTypeStr.begin(), devTypeStr.end(), devTypeStr.begin(),
            [](unsigned char c) { return static_cast<char>(std::toupper(c)); });

        auto devIt = device_map.find(devTypeStr);
        if (devIt == device_map.end()) {
            throw std::invalid_argument("Invalid device type: " + devTypeStr);
        }
        const DeviceInfo& info = devIt->second;

        // Python send_full_data_byte uses headdevice[1:6] (max 5 chars)
        std::string addrStr;
        if (headdevice.size() > 1) {
            size_t maxLen = std::min(static_cast<size_t>(5), headdevice.size() - 1);
            addrStr = headdevice.substr(1, maxLen);
        }
        else {
            throw std::invalid_argument("No address after device in headdevice: " + headdevice);
        }

        int addr = 0;
        try {
            addr = std::stoi(addrStr, nullptr, info.base);
        }
        catch (...) {
            throw std::invalid_argument("Invalid address format: " + headdevice);
        }

        auto baseIt = base_packets.find(type);
        if (baseIt == base_packets.end()) {
            throw std::invalid_argument("Unknown packet type: " + type);
        }

        std::vector<uint8_t> packet = baseIt->second;

        // Append Start Number (3 bytes little-endian)
        packet.push_back(static_cast<uint8_t>(addr & 0xFF));
        packet.push_back(static_cast<uint8_t>((addr >> 8) & 0xFF));
        packet.push_back(static_cast<uint8_t>((addr >> 16) & 0xFF));

        // Append Device Code
        packet.push_back(info.code);

        // Append Length (number of points) (2 bytes little-endian)
        packet.push_back(static_cast<uint8_t>(length & 0xFF));
        packet.push_back(static_cast<uint8_t>((length >> 8) & 0xFF));

        // Append Write Data if any
        if (!writeData.empty()) {
            packet.insert(packet.end(), writeData.begin(), writeData.end());

            // Python only patches length for writes (data_list != b"")
            int requestLen = static_cast<int>(packet.size()) - 9;
            packet[7] = static_cast<uint8_t>(requestLen & 0xFF);
            packet[8] = static_cast<uint8_t>((requestLen >> 8) & 0xFF);
        }

        return packet;
    }

    std::vector<uint8_t> receiveResponse(int expectedPoints, const std::string& type) {
        std::vector<uint8_t> buffer;
        buffer.reserve(64);
        char tmp[4096];

        // Read at least 11 bytes
        int received = 0;
        while (received < 11) {
            int r = recv(sock, tmp, sizeof(tmp), 0);
            if (r <= 0) {
                throw std::runtime_error("Receive failed or connection closed");
            }
            buffer.insert(buffer.end(), tmp, tmp + r);
            received += r;
        }

        if (buffer.size() < 11) {
            throw std::runtime_error("Incomplete PLC response header");
        }

        // EndCode (bytes 9-10)
        uint16_t endCode = static_cast<uint16_t>(buffer[9]) |
            static_cast<uint16_t>(buffer[10] << 8);
        if (endCode != 0) {
            std::stringstream ss;
            ss << "PLC Error: C"
                << std::hex << std::uppercase << std::setw(3) << std::setfill('0')
                << endCode;
            throw std::runtime_error(ss.str());
        }

        // For writes, Python just checks error and returns "OK" � here, we just stop.
        if (type == "write_response") {
            return buffer;
        }

        // Compute expected data bytes (Python formulas reduce to this)
        int expectedDataBytes = 0;
        if (type == "read_word") {
            expectedDataBytes = expectedPoints * 2;           // words or dwords (length adjusted at call)
        }
        else if (type == "read_bit") {
            expectedDataBytes = (expectedPoints + 1) / 2;     // 2 bits per byte (nibble encoding)
        }

        int totalExpected = 11 + expectedDataBytes;
        while (static_cast<int>(buffer.size()) < totalExpected) {
            int r = recv(sock, tmp, sizeof(tmp), 0);
            if (r <= 0) {
                break;
            }
            buffer.insert(buffer.end(), tmp, tmp + r);
        }

        return buffer;
    }

    template <typename T>
    std::vector<T> parseWordResponse(const std::vector<uint8_t>& buffer, int count) {
        std::vector<T> result;
        result.reserve(count);
        // Data starts at index 11
        for (int i = 0; i < count; ++i) {
            int offset = 11 + i * 2;
            if (offset + 1 >= static_cast<int>(buffer.size())) {
                break;
            }
            uint16_t raw = static_cast<uint16_t>(buffer[offset]) |
                static_cast<uint16_t>(buffer[offset + 1] << 8);
            result.push_back(static_cast<T>(static_cast<int16_t>(raw)));
        }
        return result;
    }

    template <typename T>
    std::vector<T> parseDWordResponse(const std::vector<uint8_t>& buffer, int count) {
        std::vector<T> result;
        result.reserve(count);
        // Data starts at index 11. Each DWord is 4 bytes.
        for (int i = 0; i < count; ++i) {
            int offset = 11 + i * 4;
            if (offset + 3 >= static_cast<int>(buffer.size())) {
                break;
            }
            uint32_t raw = static_cast<uint32_t>(buffer[offset]) |
                (static_cast<uint32_t>(buffer[offset + 1]) << 8) |
                (static_cast<uint32_t>(buffer[offset + 2]) << 16) |
                (static_cast<uint32_t>(buffer[offset + 3]) << 24);
            result.push_back(static_cast<T>(static_cast<int32_t>(raw)));
        }
        return result;
    }

    // Replicates Python's weird str(bytes) + digit-filter behaviour
    std::vector<int> parseBitResponse(const std::vector<uint8_t>& buffer, int count) {
        std::vector<int> result;

        if (buffer.size() <= 11) {
            // No data
            result.assign(count, 0);
            return result;
        }

        // Build string like Python's str(binary_answer[11:])
        std::stringstream ss;
        ss << "b'";
        for (size_t i = 11; i < buffer.size(); ++i) {
            ss << "\\x"
                << std::hex << std::nouppercase
                << std::setw(2) << std::setfill('0')
                << static_cast<int>(buffer[i]);
        }
        ss << "'";

        std::string dataStr = ss.str();

        std::vector<int> binDigits;
        binDigits.reserve(dataStr.size());
        for (char c : dataStr) {
            if (c == '0') {
                binDigits.push_back(0);
            }
            else if (c == '1') {
                binDigits.push_back(1);
            }
        }

        for (int i = 0; i < count && i < static_cast<int>(binDigits.size()); ++i) {
            result.push_back(binDigits[i]);
        }

        // If fewer bits than requested, pad with 0 like a safe default
        while (static_cast<int>(result.size()) < count) {
            result.push_back(0);
        }

        return result;
    }
};

#endif // MCPROTOCOL_H
