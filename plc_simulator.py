import socket
import struct
import time
import random

# Configuration
HOST = '127.0.0.1'
PORT = 6000

def create_response(data_bytes):
    # Fixed Header for Response (Subheader D0 00 ...)
    # Network(0), PC(FF), IO(FF 03), Station(0)
    header = b'\xD0\x00\x00\xFF\xFF\x03\x00'
    
    # Data Length = EndCode(2) + len(data_bytes)
    total_len = 2 + len(data_bytes)
    len_bytes = struct.pack('<H', total_len)
    
    # EndCode = 0 (Success)
    end_code = b'\x00\x00'
    
    return header + len_bytes + end_code + data_bytes

def handle_client(conn, addr):
    print(f"[+] Connected by {addr}")
    try:
        while True:
            data = conn.recv(1024)
            if not data:
                break
            
            # Basic validation of MC Protocol 3E Frame (Binary)
            if len(data) < 15 or data[0] != 0x50:
                print(f"[-] Invalid or short packet: {data.hex()}")
                continue

            # Parse Command (Bytes 11-12)
            # Command: 04 01 (Read), 14 01 (Write) - Little Endian in spec, but sent as 01 04 in C++ array
            # C++: 0x01, 0x04 -> 0x0401
            cmd_low = data[11]
            cmd_high = data[12]
            
            # Parse Subcommand (Bytes 13-14)
            # 00 00 (Word), 01 00 (Bit)
            sub_low = data[13]
            sub_high = data[14]

            # Parse Device Code (Byte 18) - for logging
            # D=0xA8, Y=0x9D, etc.
            if len(data) > 18:
                dev_code = data[18]
            else:
                dev_code = 0

            # Parse Point Count (Bytes 19-20)
            if len(data) > 20:
                points = struct.unpack('<H', data[19:21])[0]
            else:
                points = 0

            response_data = b''

            # --- LOGIC ---
            
            # READ COMMAND (0x0401)
            if cmd_low == 0x01 and cmd_high == 0x04:
                # Word Read
                if sub_low == 0x00:
                    print(f"[*] Read Word Request (Dev: {hex(dev_code)}, Count: {points})")
                    # Generate random word data (2 bytes per point)
                    # If reading D0 (Device A8), let's return a fluctuating value or incrementing value
                    # just to show activity in the dashboard.
                    for _ in range(points):
                        val = random.randint(100, 200) 
                        response_data += struct.pack('<H', val)
                
                # Bit Read
                elif sub_low == 0x01:
                    print(f"[*] Read Bit Request (Dev: {hex(dev_code)}, Count: {points})")
                    # 1 byte per 2 points (approx)
                    # Python logic in C++ expects enough nibbles.
                    byte_count = (points + 1) // 2
                    response_data = b'\x11' * byte_count # All ON

            # WRITE COMMAND (0x1401)
            elif cmd_low == 0x01 and cmd_high == 0x14:
                if sub_low == 0x00:
                    print(f"[*] Write Word Request (Dev: {hex(dev_code)}, Count: {points})")
                elif sub_low == 0x01:
                    print(f"[*] Write Bit Request (Dev: {hex(dev_code)}, Count: {points})")
                
                # Write command just returns EndCode (no data)
                response_data = b''
            
            else:
                print(f"[?] Unknown Command: {hex(cmd_low)} {hex(cmd_high)}")
                # Echo empty success for now to prevent crash
                response_data = b''

            # Send Response
            packet = create_response(response_data)
            conn.sendall(packet)

    except ConnectionResetError:
        print("[-] Connection reset by peer")
    except Exception as e:
        print(f"[-] Error: {e}")
    finally:
        conn.close()
        print("[-] Connection closed")

def main():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        # Allow address reuse
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        s.bind((HOST, PORT))
        s.listen()
        print(f"=== Mock PLC Server Listening on {HOST}:{PORT} ===")
        print("Press Ctrl+C to stop.")
        
        while True:
            try:
                conn, addr = s.accept()
                handle_client(conn, addr)
            except KeyboardInterrupt:
                print("\nStopping server...")
                break
            except Exception as e:
                print(f"Server error: {e}")

if __name__ == "__main__":
    main()
