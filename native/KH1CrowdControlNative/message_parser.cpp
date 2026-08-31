#include "pch.h"
#include "message_parser.h"

// Pulls an integer field of a supplied JSON string by key
bool ExtractIntField(const std::string& msg, const char* key, long long& outValue) {
    std::string needle = std::string("\"") + key + "\":";
    size_t searchFrom = 0;
    while (true) {
        size_t pos = msg.find(needle, searchFrom);
        if (pos == std::string::npos) return false;
        size_t valueStart = pos + needle.size();
        bool neg = false;
        size_t p = valueStart;
        if (p < msg.size() && msg[p] == '-') { neg = true; p++; }
        size_t digitsStart = p;
        while (p < msg.size() && msg[p] >= '0' && msg[p] <= '9') p++;
        if (p > digitsStart) {
            outValue = std::stoll(msg.substr(digitsStart, p - digitsStart));
            if (neg) outValue = -outValue;
            return true;
        }
        searchFrom = pos + needle.size();
    }
}

// Pulls a string field of a supplied JSON string by key
bool ExtractStringField(const std::string& msg, const char* key, std::string& outValue) {
    std::string needle = std::string("\"") + key + "\":\"";
    size_t pos = msg.find(needle);
    if (pos == std::string::npos) return false;
    pos += needle.size();
    size_t end = msg.find('"', pos);
    if (end == std::string::npos) return false;
    outValue = msg.substr(pos, end - pos);
    return true;
}
