# GEMINI Project Context Report

**Date:** 2025-12-09
**Project:** SSApp (Smart Scan Application)
**Frameworks:** .NET 10.0 (WPF), C++20 (Native DLL), SQLite

## 1. Executive Summary
SSApp is a desktop application designed for controlling and monitoring industrial machinery via Mitsubishi PLCs (Programmable Logic Controllers). It features a modern WPF-based UI for user interaction and a high-performance C++ Native DLL for communicating with the PLC hardware using the MELSEC Communication (MC) Protocol.

## 2. System Architecture

### 2.1. Frontend (`SSApp.UI`)
*   **Technology:** WPF (Windows Presentation Foundation)
*   **Theme:** Dark mode, custom window chrome, "glassmorphism" effects.
*   **Key Windows:**
    *   `DashboardWindow`: Main hub with status cards and navigation.
    *   `LoginWindow`: Authentication entry point.
    *   `ManageUsersWindow`: Admin interface for user CRUD operations.
    *   `PlcSettingsWindow` (Code-behind `PlcConnectionWindow.xaml`): Configuration for PLC IP/Port.

### 2.2. Services (`SSApp.Services`)
*   Acts as the bridge between UI and Data/Native layers.
*   `AuthService`: Manages user login state and Role-Based Access Control (RBAC).
*   `PlcConfigService`: Retreives and saves PLC connection settings.
*   `UserService`: Handles database operations for user management.

### 2.3. Data (`SSApp.Data`)
*   **Storage:** SQLite database (`ssapp.db`).
*   **Models:** `User`, `UserRole` (Enum: Admin, Operator, Viewer), `PlcConfig`.
*   **Responsibilities:** Initialization of tables, CRUD operations.

### 2.4. Native Control (`SSApp.Native`)
*   **Technology:** C++ Dynamic Link Library (DLL).
*   **Protocol:** MC Protocol (Mitsubishi) via TCP/IP.
*   **Components:**
    *   `PlcControl.cpp`: Exports `ConnectPlc`, `DisconnectPlc`, `StartScanNative`. Manages a persistent background polling thread.
    *   `mcProtocol.h`: A robust C++ class wrapping socket operations and MC Protocol packet construction/parsing.
    *   `framework.h`: Standard Windows/Winsock includes.

## 3. Current Implementation Status

### 3.1. PLC Communication
*   **Connection Model:** Persistent TCP connection.
*   **Library:** Refactored to use `MCProtocol` helper class (replacing raw socket code).
*   **Polling:** A background thread polls device `D0` every 500ms to verify connection health.
*   **Operations:**
    *   **Connect:** Establishes connection and starts polling.
    *   **Start Scan:** Sends `Y1` ON -> Waits 5s -> Sends `Y1` OFF (Asynchronous detached thread).

### 3.2. User Interface & Experience
*   **Dashboard:**
    *   Displays real-time PLC connection status (Green/Red indicator).
    *   Role-based visibility for "Manage Users", "Settings", and "Start Scan" buttons.
*   **Settings:**
    *   Modal dialog to configure PLC IP and Port.
    *   Upon saving, automatically attempts to reconnect and updates the Dashboard status.
*   **User Management:**
    *   Admin-only access.
    *   Grid view of users.
    *   **Shortcut:** Enter key binds to the "Save" action.

### 3.3. Authentication
*   **Roles:**
    *   **Admin:** Full access (Users, Settings, Scan).
    *   **Operator:** Operational access (Settings, Scan).
    *   **Viewer:** Read-only access (Status viewing).
*   **Default Credentials:** Seeded in `SSApp.Data/Database.cs` (e.g., `admin`/`admin123`).

## 4. Recent Code Changes
1.  **Native Refactoring:** Swapped raw socket implementation in `PlcControl.cpp` with the structured `MCProtocol` class.
2.  **Thread Safety:** Added `std::mutex` (`g_PlcMutex`) in C++ to protect the persistent `MCProtocol` instance from concurrent access (Polling vs. Command execution).
3.  **UI Wiring:** Connected `PlcConnectionWindow` to the dashboard's PLC status card. Saving settings now triggers an immediate reconnection attempt.
4.  **UX Improvements:** Added "Enter to Save" in the Manage Users window.
5.  **Machine Status:** Wired the "Machine Status" card on the dashboard to the `D0` register via a polling loop.
6.  **Past Scans:** Implemented `PastScansWindow` with a backing SQLite table (`ScanResults`) and service layer.
7.  **Error Handling:** Implemented `NotificationService` (Toast Toasts) and `FileLogger` (Text logs) in `SSApp.Services`. Replaced `MessageBox` alerts in `DashboardWindow` with non-blocking notifications.
8.  **C++ Build Fix:** Resolved `min`/`max` macro conflicts in `SSApp.Native` by adding `#define NOMINMAX` to `framework.h`.

## 5. Known Limitations & Next Steps
*   **Machine Status Window:** The `MachineStatusWindow` (popup) is currently empty. The dashboard card works, but the detailed view is not implemented.
*   **Scan Logic:** The current "Scan" is a hardcoded 5-second toggle of `Y1`. This will need to be replaced with the actual business logic for the machine scan cycle.

## 6. User Preferences
*   The user prefers to build the application themselves. The agent should NOT execute `dotnet build` or similar commands.
