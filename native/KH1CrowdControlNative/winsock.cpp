#include "pch.h"
#include "log.h"
#include "winsock.h"

#pragma comment(lib, "ws2_32.lib")

static bool g_wsaInitialized = false;

// Initializes Windows Sockets API
bool EnsureWinsock() {
    if (g_wsaInitialized) return true;
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        LogDebug("EnsureWinsock: WSAStartup failed");
        return false;
    }
    g_wsaInitialized = true;
    return true;
}

// Closes Windows Sockets API cleanly
void CleanupWinsock() {
    if (g_wsaInitialized) {
        WSACleanup();
        g_wsaInitialized = false;
    }
}
