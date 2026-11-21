#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <UniversalDevKit/Diagnostic/Logging.h>

#include "HttpServer.h"

#include <string_view>
#include <filesystem>
#include <stdexcept>
#include <iostream>
#include <cstdlib>
#include <cstdio>
#include <string>

namespace unitoffice::windows::httppingserver {
	void InitializeLogging() {
		universaldevkit::diagnostic::LoggerOptions loggerOptions;
		loggerOptions.filePath = std::filesystem::temp_directory_path() / L"UnitOffice.AscodInteropServer.log";
		loggerOptions.enableConsole = true;
		const auto result = universaldevkit::diagnostic::Logger::Initialize(loggerOptions);
		if (!result) {
			throw std::runtime_error("Could not initialize Diagnostic logging: " + result.message);
		}
		UDK_LOG_INFO("HttpPingServer", L"Log file: {}", loggerOptions.filePath.native());
	}

	void Run(int argc, wchar_t* argv[]) {
		constexpr std::wstring_view defaultAddress = L"192.168.202.46";
		const std::wstring_view option = argc > 1 ? argv[argc - 1] : L"";
		const bool hasProtocolOption = option == L"--https" || option == L"--http";
		const bool useHttps = option != L"--http";
		const int addressCount = argc - (hasProtocolOption ? 1 : 0);
		const std::wstring_view address = addressCount == 2 ? argv[1] : defaultAddress;
		if (addressCount > 2 || address.empty() || address.find_first_not_of(L"0123456789.") != std::wstring_view::npos) {
			throw std::runtime_error("Usage: UnitOffice.AscodInteropServer.exe [IPv4-address] [--https|--http]");
		}

		std::string accessKey;
#ifdef _DEBUG
		accessKey = "unitoffice-local-test-2026";
#else
		std::fprintf(stderr, "Enter the shared chat password: ");
		std::fflush(stderr);
		if (!std::getline(std::cin, accessKey)) {
			throw std::runtime_error("Could not read the chat password.");
		}
#endif
		if (accessKey.size() < 16 || accessKey.size() > 1023 ||
			accessKey.find_first_not_of("!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~") != std::string::npos) {
			throw std::runtime_error("Use 16 to 1023 printable ASCII characters without spaces for the chat password.");
		}

		const std::wstring url = (useHttps ? L"https://" : L"http://") + std::wstring(address) + (useHttps ? L":8443/" : L":8080/");
		Router router(accessKey);
		HttpServer::Run(url, router);
	}
}

int wmain(int argc, wchar_t* argv[]) {
	int exitCode = EXIT_SUCCESS;
	bool loggingInitialized = false;
	try {
		unitoffice::windows::httppingserver::InitializeLogging();
		loggingInitialized = true;
		unitoffice::windows::httppingserver::Run(argc, argv);
	} catch (const std::exception& error) {
		if (loggingInitialized) {
			UDK_LOG_ERROR("HttpPingServer", "{}", error.what());
		} else {
			std::fprintf(stderr, "%s\n", error.what());
		}
		exitCode = EXIT_FAILURE;
	} catch (...) {
		if (loggingInitialized) {
			UDK_LOG_ERROR("HttpPingServer", "Unknown error");
		} else {
			std::fprintf(stderr, "Unknown error\n");
		}
		exitCode = EXIT_FAILURE;
	}

	if (loggingInitialized) {
		universaldevkit::diagnostic::Logger::Shutdown();
	}
	std::system("pause");
	return exitCode;
}