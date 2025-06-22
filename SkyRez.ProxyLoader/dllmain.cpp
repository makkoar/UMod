// -----------------------------------------------------------------------------
// Предкомпилированный заголовок (должен быть первым)
// -----------------------------------------------------------------------------
#include "pch.h"

#pragma region Includes and Usings
// -----------------------------------------------------------------------------
// Основные системные и стандартные заголовки
// -----------------------------------------------------------------------------
#include <windows.h>   // Для Windows API
#include <string>      // Для std::string
#include <filesystem>  // Для std::filesystem (требует C++17)
// #include <fstream>  // Больше не нужен, так как логгер удален

// -----------------------------------------------------------------------------
// Заголовки для COM и .NET хостинга
// -----------------------------------------------------------------------------
#include <atlbase.h>   // Для CComPtr, CComBSTR, CComVariant
#include <comutil.h>   // Для _variant_t, _bstr_t (и comsuppw.lib)
#include <metahost.h>  // Для ICLRMetaHost, ICLRRuntimeInfo, ICorRuntimeHost

// -----------------------------------------------------------------------------
// Подключение библиотек
// -----------------------------------------------------------------------------
#pragma comment(lib, "mscoree.lib")    // Основная библиотека .NET Runtime
#pragma comment(lib, "comsuppw.lib") // Для _variant_t, _bstr_t (если используются)

// -----------------------------------------------------------------------------
// Импорт библиотеки типов .NET (mscorlib)
// -----------------------------------------------------------------------------
#import "mscorlib.tlb" raw_interfaces_only \
    high_property_prefixes("_get","_put","_putref") \
    rename("ReportEvent", "InteropServices_ReportEvent") \
    rename("or", "mscorlib_or")

// -----------------------------------------------------------------------------
// Объявление используемых пространств имен
// -----------------------------------------------------------------------------
using namespace std;
using namespace mscorlib;
#pragma endregion

#pragma region Original Function Typedefs and Pointers
// -----------------------------------------------------------------------------
// Typedef'ы для сигнатур оригинальных функций и указатели на них
// -----------------------------------------------------------------------------
HMODULE hOriginalDll = NULL;

typedef DWORD(WINAPI* FnGetFileVersionInfoSizeA)(LPCSTR lptstrFilename, LPDWORD lpdwHandle);
FnGetFileVersionInfoSizeA pGetFileVersionInfoSizeA = NULL;
typedef BOOL(WINAPI* FnGetFileVersionInfoA)(LPCSTR lptstrFilename, DWORD dwHandle, DWORD dwLen, LPVOID lpData);
FnGetFileVersionInfoA pGetFileVersionInfoA = NULL;
typedef DWORD(WINAPI* FnGetFileVersionInfoSizeW)(LPCWSTR lptstrFilename, LPDWORD lpdwHandle);
FnGetFileVersionInfoSizeW pGetFileVersionInfoSizeW = NULL;
typedef BOOL(WINAPI* FnGetFileVersionInfoW)(LPCWSTR lptstrFilename, DWORD dwHandle, DWORD dwLen, LPVOID lpData);
FnGetFileVersionInfoW pGetFileVersionInfoW = NULL;
typedef DWORD(WINAPI* FnGetFileVersionInfoSizeExA)(DWORD dwFlags, LPCSTR lpwstrFilename, LPDWORD lpdwHandle);
FnGetFileVersionInfoSizeExA pGetFileVersionInfoSizeExA = NULL;
typedef BOOL(WINAPI* FnGetFileVersionInfoExA)(DWORD dwFlags, LPCSTR lpwstrFilename, DWORD dwHandle, DWORD dwLen, LPVOID lpData);
FnGetFileVersionInfoExA pGetFileVersionInfoExA = NULL;
typedef DWORD(WINAPI* FnGetFileVersionInfoSizeExW)(DWORD dwFlags, LPCWSTR lpwstrFilename, LPDWORD lpdwHandle);
FnGetFileVersionInfoSizeExW pGetFileVersionInfoSizeExW = NULL;
typedef BOOL(WINAPI* FnGetFileVersionInfoExW)(DWORD dwFlags, LPCWSTR lpwstrFilename, DWORD dwHandle, DWORD dwLen, LPVOID lpData);
FnGetFileVersionInfoExW pGetFileVersionInfoExW = NULL;
typedef DWORD(WINAPI* FnVerFindFileA)(DWORD uFlags, LPCSTR szFileName, LPCSTR szWinDir, LPCSTR szAppDir, LPSTR szCurDir, PUINT lpuCurDirLen, LPSTR szDestDir, PUINT lpuDestDirLen);
FnVerFindFileA pVerFindFileA = NULL;
typedef DWORD(WINAPI* FnVerFindFileW)(DWORD uFlags, LPCWSTR szFileName, LPCWSTR szWinDir, LPCWSTR szAppDir, LPWSTR szCurDir, PUINT lpuCurDirLen, LPWSTR szDestDir, PUINT lpuDestDirLen);
FnVerFindFileW pVerFindFileW = NULL;
typedef DWORD(WINAPI* FnVerInstallFileA)(DWORD uFlags, LPCSTR szSrcFileName, LPCSTR szDestFileName, LPCSTR szSrcDir, LPCSTR szDestDir, LPCSTR szCurDir, LPSTR szTmpFile, PUINT lpuTmpFileLen);
FnVerInstallFileA pVerInstallFileA = NULL;
typedef DWORD(WINAPI* FnVerInstallFileW)(DWORD uFlags, LPCWSTR szSrcFileName, LPCWSTR szDestFileName, LPCWSTR szSrcDir, LPCWSTR szDestDir, LPCWSTR szCurDir, LPWSTR szTmpFile, PUINT lpuTmpFileLen);
FnVerInstallFileW pVerInstallFileW = NULL;
typedef DWORD(WINAPI* FnVerLanguageNameA)(WORD wLang, LPSTR szLang, DWORD nSize);
FnVerLanguageNameA pVerLanguageNameA = NULL;
typedef DWORD(WINAPI* FnVerLanguageNameW)(WORD wLang, LPWSTR szLang, DWORD nSize);
FnVerLanguageNameW pVerLanguageNameW = NULL;
typedef BOOL(WINAPI* FnVerQueryValueA)(LPCVOID pBlock, LPCSTR lpSubBlock, LPVOID* lplpBuffer, PUINT puLen);
FnVerQueryValueA pVerQueryValueA = NULL;
typedef BOOL(WINAPI* FnVerQueryValueW)(LPCVOID pBlock, LPCWSTR lpSubBlock, LPVOID* lplpBuffer, PUINT puLen);
FnVerQueryValueW pVerQueryValueW = NULL;
#pragma endregion

#pragma region Mod Initialization Thread
// Поток для инициализации .NET мода
DWORD WINAPI InitializeMod(LPVOID lpParam)
{
    HMODULE hSelf = (HMODULE)lpParam;
    if (!hSelf) { return -1; } // Не удалось получить хэндл текущего модуля

    // Определение путей и имени игры
    char selfPathChars[MAX_PATH] = { 0 };
    GetModuleFileNameA(hSelf, selfPathChars, MAX_PATH);
    filesystem::path gameRootPath = filesystem::path(selfPathChars).parent_path();

    char exePathChars[MAX_PATH] = { 0 };
    GetModuleFileNameA(NULL, exePathChars, MAX_PATH);
    string gameNameWithoutExt = filesystem::path(exePathChars).stem().string();

    HRESULT hr;
    CComPtr<ICLRMetaHost> pMetaHost;
    CComPtr<ICLRRuntimeInfo> pRuntimeInfo;
    CComPtr<ICorRuntimeHost> pCorRuntimeHost;

    // Шаг 1: Получение ICLRMetaHost
    hr = CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (LPVOID*)&pMetaHost);
    if (FAILED(hr)) { return 1; }

    // Шаг 2: Получение ICLRRuntimeInfo для v2.0.50727
    hr = pMetaHost->GetRuntime(L"v2.0.50727", IID_ICLRRuntimeInfo, (LPVOID*)&pRuntimeInfo);
    if (FAILED(hr)) { return 2; }

    // Шаг 3: Получение ICorRuntimeHost
    hr = pRuntimeInfo->GetInterface(CLSID_CorRuntimeHost, IID_ICorRuntimeHost, (LPVOID*)&pCorRuntimeHost);
    if (FAILED(hr)) { return 3; }

    // Шаг 4: Запуск CLR
    hr = pCorRuntimeHost->Start();
    if (FAILED(hr)) { return 4; }

    // Шаг 5: Настройка AppDomain (установка PRIVATE_BINPATH)
    CComPtr<IUnknown> pAppDomainThunk;
    CComPtr<_AppDomain> pDefaultAppDomain;
    hr = pCorRuntimeHost->GetDefaultDomain(&pAppDomainThunk);
    if (FAILED(hr)) { return 5; }
    hr = pAppDomainThunk->QueryInterface(__uuidof(_AppDomain), (LPVOID*)&pDefaultAppDomain);
    if (FAILED(hr)) { return 5; }

    string managedPathStr = gameNameWithoutExt + "_Data\\Managed";
    CComBSTR privateBinPath(managedPathStr.c_str());
    hr = pDefaultAppDomain->AppendPrivatePath(privateBinPath);
    if (FAILED(hr)) { return 5; }

    // Шаг 6: Создание экземпляра .NET класса и вызов метода
    CComPtr<_ObjectHandle> pObjectHandle;
    CComBSTR assemblyNameBSTR(L"SkyRez.Translate");
    CComBSTR classNameBSTR(L"SkyRez.Translate.ComBridge");

    hr = pDefaultAppDomain->CreateInstance(assemblyNameBSTR, classNameBSTR, &pObjectHandle);
    if (FAILED(hr) || pObjectHandle == nullptr) { return 6; }

    CComVariant objFromHandle;
    hr = pObjectHandle->Unwrap(&objFromHandle);
    if (FAILED(hr) || objFromHandle.vt != VT_DISPATCH) { return 6; }

    IDispatch* pDisp = objFromHandle.pdispVal;
    if (!pDisp) { return 6; }

    DISPID dispidBootstrap;
    OLECHAR* bootstrapMethodName = SysAllocString(L"Bootstrap");
    hr = pDisp->GetIDsOfNames(IID_NULL, &bootstrapMethodName, 1, LOCALE_USER_DEFAULT, &dispidBootstrap);
    SysFreeString(bootstrapMethodName);
    if (FAILED(hr)) { return 6; }

    DISPPARAMS dp = { NULL, NULL, 0, 0 };
    CComVariant result;
    hr = pDisp->Invoke(dispidBootstrap, IID_NULL, LOCALE_USER_DEFAULT, DISPATCH_METHOD, &dp, &result, NULL, NULL);
    if (FAILED(hr)) { return 6; }

    // Завершение работы с CLR (если мод работает в основном потоке и не требует CLR после инициализации)
    // В данном случае, COM объект и .NET код будут жить, пока жив AppDomain, остановка здесь не нужна
    // pCorRuntimeHost->Stop(); // Раскомментировать, если CLR больше не нужен

    return 0; // Успешное завершение инициализации мода
}
#pragma endregion

#pragma region DllMain
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    {
        DisableThreadLibraryCalls(hModule);

        // Загрузка оригинальной DLL и получение адресов функций
        char systemPath[MAX_PATH];
        GetSystemDirectoryA(systemPath, MAX_PATH);
        strcat_s(systemPath, sizeof(systemPath), "\\version.dll");
        hOriginalDll = LoadLibraryA(systemPath);
        if (!hOriginalDll) return FALSE; // Не удалось загрузить оригинальную DLL

        // Получение адресов всех экспортируемых функций
        pGetFileVersionInfoSizeA = (FnGetFileVersionInfoSizeA)GetProcAddress(hOriginalDll, "GetFileVersionInfoSizeA");
        pGetFileVersionInfoA = (FnGetFileVersionInfoA)GetProcAddress(hOriginalDll, "GetFileVersionInfoA");
        pGetFileVersionInfoSizeW = (FnGetFileVersionInfoSizeW)GetProcAddress(hOriginalDll, "GetFileVersionInfoSizeW");
        pGetFileVersionInfoW = (FnGetFileVersionInfoW)GetProcAddress(hOriginalDll, "GetFileVersionInfoW");
        pGetFileVersionInfoSizeExA = (FnGetFileVersionInfoSizeExA)GetProcAddress(hOriginalDll, "GetFileVersionInfoSizeExA");
        pGetFileVersionInfoExA = (FnGetFileVersionInfoExA)GetProcAddress(hOriginalDll, "GetFileVersionInfoExA");
        pGetFileVersionInfoSizeExW = (FnGetFileVersionInfoSizeExW)GetProcAddress(hOriginalDll, "GetFileVersionInfoSizeExW");
        pGetFileVersionInfoExW = (FnGetFileVersionInfoExW)GetProcAddress(hOriginalDll, "GetFileVersionInfoExW");
        pVerFindFileA = (FnVerFindFileA)GetProcAddress(hOriginalDll, "VerFindFileA");
        pVerFindFileW = (FnVerFindFileW)GetProcAddress(hOriginalDll, "VerFindFileW");
        pVerInstallFileA = (FnVerInstallFileA)GetProcAddress(hOriginalDll, "VerInstallFileA");
        pVerInstallFileW = (FnVerInstallFileW)GetProcAddress(hOriginalDll, "VerInstallFileW");
        pVerLanguageNameA = (FnVerLanguageNameA)GetProcAddress(hOriginalDll, "VerLanguageNameA");
        pVerLanguageNameW = (FnVerLanguageNameW)GetProcAddress(hOriginalDll, "VerLanguageNameW");
        pVerQueryValueA = (FnVerQueryValueA)GetProcAddress(hOriginalDll, "VerQueryValueA");
        pVerQueryValueW = (FnVerQueryValueW)GetProcAddress(hOriginalDll, "VerQueryValueW");

        // Создание потока для инициализации мода
        HANDLE hThread = CreateThread(NULL, 0, InitializeMod, hModule, 0, NULL);
        if (hThread)
        {
            CloseHandle(hThread); // Закрываем хэндл, так как он нам больше не нужен
        }
        else
        {
            // Обработка ошибки создания потока (если необходимо, хотя обычно это просто означает, что мод не загрузится)
        }
        break;
    }
    case DLL_PROCESS_DETACH:
    {
        if (hOriginalDll)
        {
            FreeLibrary(hOriginalDll);
            hOriginalDll = NULL;
        }
        break;
    }
    }
    return TRUE;
}
#pragma endregion

#pragma region Exported Proxy Functions
// Реализация прокси-функций, перенаправляющих вызовы к оригинальной DLL

extern "C" DWORD WINAPI Proxy_GetFileVersionInfoSizeA(LPCSTR lptstrFilename, LPDWORD lpdwHandle) {
    if (pGetFileVersionInfoSizeA) return pGetFileVersionInfoSizeA(lptstrFilename, lpdwHandle); return 0;
}
extern "C" BOOL WINAPI Proxy_GetFileVersionInfoA(LPCSTR lptstrFilename, DWORD dwHandle, DWORD dwLen, LPVOID lpData) {
    if (pGetFileVersionInfoA) return pGetFileVersionInfoA(lptstrFilename, dwHandle, dwLen, lpData); return FALSE;
}
extern "C" DWORD WINAPI Proxy_GetFileVersionInfoSizeW(LPCWSTR lptstrFilename, LPDWORD lpdwHandle) {
    if (pGetFileVersionInfoSizeW) return pGetFileVersionInfoSizeW(lptstrFilename, lpdwHandle); return 0;
}
extern "C" BOOL WINAPI Proxy_GetFileVersionInfoW(LPCWSTR lptstrFilename, DWORD dwHandle, DWORD dwLen, LPVOID lpData) {
    if (pGetFileVersionInfoW) return pGetFileVersionInfoW(lptstrFilename, dwHandle, dwLen, lpData); return FALSE;
}
extern "C" DWORD APIENTRY Proxy_GetFileVersionInfoSizeExA(DWORD dwFlags, LPCSTR lpwstrFilename, LPDWORD lpdwHandle) {
    if (pGetFileVersionInfoSizeExA) return pGetFileVersionInfoSizeExA(dwFlags, lpwstrFilename, lpdwHandle); return 0;
}
extern "C" BOOL APIENTRY Proxy_GetFileVersionInfoExA(DWORD dwFlags, LPCSTR lpwstrFilename, DWORD dwHandle, DWORD dwLen, LPVOID lpData) {
    if (pGetFileVersionInfoExA) return pGetFileVersionInfoExA(dwFlags, lpwstrFilename, dwHandle, dwLen, lpData); return FALSE;
}
extern "C" DWORD APIENTRY Proxy_GetFileVersionInfoSizeExW(DWORD dwFlags, LPCWSTR lpwstrFilename, LPDWORD lpdwHandle) {
    if (pGetFileVersionInfoSizeExW) return pGetFileVersionInfoSizeExW(dwFlags, lpwstrFilename, lpdwHandle); return 0;
}
extern "C" BOOL APIENTRY Proxy_GetFileVersionInfoExW(DWORD dwFlags, LPCWSTR lpwstrFilename, DWORD dwHandle, DWORD dwLen, LPVOID lpData) {
    if (pGetFileVersionInfoExW) return pGetFileVersionInfoExW(dwFlags, lpwstrFilename, dwHandle, dwLen, lpData); return FALSE;
}
extern "C" DWORD APIENTRY Proxy_VerFindFileA(DWORD uFlags, LPCSTR szFileName, LPCSTR szWinDir, LPCSTR szAppDir, LPSTR szCurDir, PUINT lpuCurDirLen, LPSTR szDestDir, PUINT lpuDestDirLen) {
    if (pVerFindFileA) return pVerFindFileA(uFlags, szFileName, szWinDir, szAppDir, szCurDir, lpuCurDirLen, szDestDir, lpuDestDirLen); return 0;
}
extern "C" DWORD APIENTRY Proxy_VerFindFileW(DWORD uFlags, LPCWSTR szFileName, LPCWSTR szWinDir, LPCWSTR szAppDir, LPWSTR szCurDir, PUINT lpuCurDirLen, LPWSTR szDestDir, PUINT lpuDestDirLen) {
    if (pVerFindFileW) return pVerFindFileW(uFlags, szFileName, szWinDir, szAppDir, szCurDir, lpuCurDirLen, szDestDir, lpuDestDirLen); return 0;
}
extern "C" DWORD APIENTRY Proxy_VerInstallFileA(DWORD uFlags, LPCSTR szSrcFileName, LPCSTR szDestFileName, LPCSTR szSrcDir, LPCSTR szDestDir, LPCSTR szCurDir, LPSTR szTmpFile, PUINT lpuTmpFileLen) {
    if (pVerInstallFileA) return pVerInstallFileA(uFlags, szSrcFileName, szDestFileName, szSrcDir, szDestDir, szCurDir, szTmpFile, lpuTmpFileLen); return 0;
}
extern "C" DWORD APIENTRY Proxy_VerInstallFileW(DWORD uFlags, LPCWSTR szSrcFileName, LPCWSTR szDestFileName, LPCWSTR szSrcDir, LPCWSTR szDestDir, LPCWSTR szCurDir, LPWSTR szTmpFile, PUINT lpuTmpFileLen) {
    if (pVerInstallFileW) return pVerInstallFileW(uFlags, szSrcFileName, szDestFileName, szSrcDir, szDestDir, szCurDir, szTmpFile, lpuTmpFileLen); return 0;
}
extern "C" DWORD APIENTRY Proxy_VerLanguageNameA(WORD wLang, LPSTR szLang, DWORD nSize) {
    if (pVerLanguageNameA) return pVerLanguageNameA(wLang, szLang, nSize); return 0;
}
extern "C" DWORD APIENTRY Proxy_VerLanguageNameW(WORD wLang, LPWSTR szLang, DWORD nSize) {
    if (pVerLanguageNameW) return pVerLanguageNameW(wLang, szLang, nSize); return 0;
}
extern "C" BOOL APIENTRY Proxy_VerQueryValueA(LPCVOID pBlock, LPCSTR lpSubBlock, LPVOID* lplpBuffer, PUINT puLen) {
    if (pVerQueryValueA) return pVerQueryValueA(pBlock, lpSubBlock, lplpBuffer, puLen); return FALSE;
}
extern "C" BOOL APIENTRY Proxy_VerQueryValueW(LPCVOID pBlock, LPCWSTR lpSubBlock, LPVOID* lplpBuffer, PUINT puLen) {
    if (pVerQueryValueW) return pVerQueryValueW(pBlock, lpSubBlock, lplpBuffer, puLen); return FALSE;
}
#pragma endregion