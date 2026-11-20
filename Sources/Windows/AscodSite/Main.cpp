#define WIN32_LEAN_AND_MEAN
#include <UniversalDevKit/Diagnostic/Logging.h>

#include <HttpHosting/HttpServer.h>

#include <filesystem>
#include <stdexcept>
#include <iterator>
#include <fstream>
#include <cstdio>

namespace unitoffice::windows::ascodsite {
	void InitializeLogging() {
		universaldevkit::diagnostic::LoggerOptions loggerOptions;
		loggerOptions.filePath = std::filesystem::temp_directory_path() / L"UnitOffice.AscodSite.log";
		loggerOptions.enableConsole = true;
		loggerOptions.colorConsole = false;
		const auto result = universaldevkit::diagnostic::Logger::Initialize(loggerOptions);
		if (!result) {
			throw std::runtime_error("Could not initialize Diagnostic logging: " + result.message);
		}
		UDK_LOG_INFO("AscodSite", L"Log file: {}", loggerOptions.filePath.native());
	}

	std::filesystem::path ExecutableDirectory() {
		std::wstring executable(32768, L'\0');
		const auto length = ::GetModuleFileNameW(nullptr, executable.data(), static_cast<DWORD>(executable.size()));
		if (length == 0 || length >= executable.size()) {
			throw std::runtime_error("Could not locate the executable.");
		}
		executable.resize(length);
		return std::filesystem::path(executable).parent_path();
	}

	httphosting::HttpResponse ReadAsset(const std::filesystem::path& path, const char* contentType) {
		std::ifstream file(path, std::ios::binary);
		if (!file) {
			UDK_LOG_ERROR("AscodSite", L"Could not open asset: {}", path.native());
			return httphosting::HttpResponse::Error(500);
		}
		return {200, {std::istreambuf_iterator<char>{file}, std::istreambuf_iterator<char>{}}, contentType};
	}

	httphosting::HttpResponse Route(const std::filesystem::path& root, const HTTP_REQUEST& request) {
		if (request.Verb != HttpVerbGET && request.Verb != HttpVerbHEAD) {
			return httphosting::HttpResponse::Error(405);
		}
		const auto& url = request.CookedUrl;
		const std::wstring_view path(url.pAbsPath, url.AbsPathLength / sizeof(wchar_t));
		if (path == L"/" || path == L"/page1" || path == L"/page1/card") {
			return ascodsite::ReadAsset(root / L"Web" / L"Page1.html", "text/html; charset=utf-8");
		}
		if (path == L"/api/v1/documents") {
			return ascodsite::ReadAsset(root / L"Data" / L"Documents.xml", "application/xml; charset=utf-8");
		}
		return httphosting::HttpResponse::Error(404);
	}
}

int wmain() {
	bool loggingInitialized = false;
	int exitCode = 0;
	try {
		unitoffice::windows::ascodsite::InitializeLogging();
		loggingInitialized = true;
		const auto root = unitoffice::windows::ascodsite::ExecutableDirectory();
		auto route = [&root](HANDLE, const HTTP_REQUEST& request) {
			return unitoffice::windows::ascodsite::Route(root, request);
		};
		UDK_LOG_INFO("AscodSite", "Registry: http://127.0.0.1:8091/page1");
		unitoffice::windows::httphosting::HttpServer::Run(L"http://127.0.0.1:8091/", route);
	} catch (const std::exception& error) {
		exitCode = 1;
		if (loggingInitialized) {
			UDK_LOG_ERROR("AscodSite", "{}", error.what());
		} else {
			std::fprintf(stderr, "AscodSite: %s\n", error.what());
		}
	}
	if (loggingInitialized) {
		universaldevkit::diagnostic::Logger::Shutdown();
	}
	return exitCode;
}