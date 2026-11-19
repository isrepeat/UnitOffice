#define WIN32_LEAN_AND_MEAN
#include "../HttpHosting/HttpServer.h"

#include <filesystem>
#include <iterator>
#include <fstream>
#include <cstdio>

namespace unitoffice::windows::ascodsite {
	using httphosting::HttpResponse;
	using httphosting::HttpServer;

	std::filesystem::path ExecutableDirectory() {
		std::wstring executable(32768, L'\0');
		const auto length = ::GetModuleFileNameW(nullptr, executable.data(), static_cast<DWORD>(executable.size()));
		if (length == 0 || length >= executable.size()) {
			throw std::runtime_error("Could not locate the executable.");
		}
		executable.resize(length);
		return std::filesystem::path(executable).parent_path();
	}

	HttpResponse ReadAsset(const std::filesystem::path& path, const char* contentType) {
		std::ifstream file(path, std::ios::binary);
		if (!file) {
			return HttpResponse::Error(500);
		}
		return {200, {std::istreambuf_iterator<char>{file}, std::istreambuf_iterator<char>{}}, contentType};
	}

	HttpResponse Route(const std::filesystem::path& root, const HTTP_REQUEST& request) {
		if (request.Verb != HttpVerbGET && request.Verb != HttpVerbHEAD) {
			return HttpResponse::Error(405);
		}
		const auto& url = request.CookedUrl;
		const std::wstring_view path(url.pAbsPath, url.AbsPathLength / sizeof(wchar_t));
		if (path == L"/" || path == L"/page1" || path == L"/page1/card") {
			return ascodsite::ReadAsset(root / L"Web" / L"Page1.html", "text/html; charset=utf-8");
		}
		if (path == L"/api/v1/documents") {
			return ascodsite::ReadAsset(root / L"Data" / L"Documents.xml", "application/xml; charset=utf-8");
		}
		return HttpResponse::Error(404);
	}
}

int wmain() {
	using namespace unitoffice::windows;
	try {
		const auto root = ascodsite::ExecutableDirectory();
		auto route = [&root](HANDLE, const HTTP_REQUEST& request) {
			return ascodsite::Route(root, request);
		};
		std::puts("AscodSite: http://127.0.0.1:8091/page1");
		httphosting::HttpServer::Run(L"http://127.0.0.1:8091/", route);
		return 0;
	} catch (const std::exception& error) {
		std::fprintf(stderr, "AscodSite: %s\n", error.what());
		return 1;
	}
}