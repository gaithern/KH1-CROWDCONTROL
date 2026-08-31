#pragma once
#include <string>

bool ExtractIntField(const std::string& msg, const char* key, long long& outValue);
bool ExtractStringField(const std::string& msg, const char* key, std::string& outValue);
