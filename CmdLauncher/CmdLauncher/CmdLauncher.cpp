// CmdLauncher.cpp : このファイルには 'main' 関数が含まれています。プログラム実行の開始と終了がそこで行われます。
//

#include <windows.h>
#include <string>
#include <iostream>
#include <vector>
#include <fstream>
#include <sstream>
#include <filesystem>

#include "Runner.h"

namespace fs = std::filesystem;

// UTF-8 文字列を UTF-16 (wstring) に変換するヘルパー関数
std::wstring Utf8ToUtf16(const std::string& utf8)
{
    int len = MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, nullptr, 0);
    if (len == 0) return L"";

    std::wstring utf16(len, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, &utf16[0], len);
    utf16.resize(len - 1); // null文字除去
    return utf16;
}

// UTF-16 → UTF-8 変換（出力用）
std::string Utf16ToUtf8(const std::wstring& utf16)
{
    int len = WideCharToMultiByte(CP_UTF8, 0, utf16.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (len == 0) return "";

    std::string utf8(len, '\0');
    WideCharToMultiByte(CP_UTF8, 0, utf16.c_str(), -1, &utf8[0], len, nullptr, nullptr);
    utf8.resize(len - 1); // null 終端を除く
    return utf8;
}

// エラーコードからエラーメッセージを取得
std::wstring GetLastErrorMessage(DWORD errorCode)
{
    LPWSTR messageBuffer = nullptr;
    DWORD size = FormatMessageW(
        FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
        NULL,
        errorCode,
        MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
        (LPWSTR)&messageBuffer,
        0,
        NULL
    );

    std::wstring message(messageBuffer, size);
    LocalFree(messageBuffer);
    return message;
}

/// 指定されたファイルが存在するかどうか
bool FileExists(const std::wstring& path)
{
    DWORD attr = GetFileAttributesW(path.c_str());
    return (attr != INVALID_FILE_ATTRIBUTES && !(attr & FILE_ATTRIBUTE_DIRECTORY));
}

/// 指定されたパスが実行可能ファイルか（Windowsでは存在すればOK）
bool IsExecutable(const std::wstring& path)
{
    return FileExists(path);
}

std::wstring GetEnvVar(const wchar_t* name)
{
    wchar_t* buffer = nullptr;
    size_t len = 0;

    if (_wdupenv_s(&buffer, &len, name) != 0 || buffer == nullptr)
    {
        return L"";
    }

    std::wstring result(buffer);
    free(buffer);  // 必須：_wdupenv_s は malloc で確保する
    return result;
}

/// 実行ファイルをカレントディレクトリおよび PATH から探してフルパスを返す
std::wstring FindExecutableInPath(const std::wstring& exeName)
{
    // 1. 絶対パス指定かどうか
    if (fs::path(exeName).is_absolute() && IsExecutable(exeName))
    {
        return fs::absolute(exeName).wstring();
    }

    // 2. PATHEXT から拡張子一覧取得（Windows専用）
    std::vector<std::wstring> extensions;
    std::wstring pathextStr = GetEnvVar(L"PATHEXT");
    if (!pathextStr.empty())
    {
        std::wstringstream ss(pathextStr);
        std::wstring ext;
        while (std::getline(ss, ext, L';'))
        {
            extensions.push_back(ext);
        }
    }
    else
    {
        extensions = { L".EXE", L".BAT", L".CMD" };
    }

    // 3. カレントディレクトリを先に確認
    {
        fs::path currentDir = fs::current_path();

        fs::path candidate = currentDir / exeName;
        if (IsExecutable(candidate.wstring()))
        {
            return fs::absolute(candidate).wstring();
        }

        for (const auto& ext : extensions)
        {
            fs::path extCandidate = currentDir / (exeName + ext);
            if (IsExecutable(extCandidate.wstring()))
            {
                return fs::absolute(extCandidate).wstring();
            }
        }
    }

    // 4. PATH を走査して検索
    std::wstring pathEnv = GetEnvVar(L"PATH");
    std::wstringstream ss(pathEnv);
    std::wstring dir;
    while (std::getline(ss, dir, L';'))
    {
        fs::path base = fs::path(dir);

        fs::path candidate = base / exeName;
        if (IsExecutable(candidate.wstring()))
        {
            return fs::absolute(candidate).wstring();
        }

        for (const auto& ext : extensions)
        {
            fs::path extCandidate = base / (exeName + ext);
            if (IsExecutable(extCandidate.wstring()))
            {
                return fs::absolute(extCandidate).wstring();
            }
        }
    }

    // 5. 見つからなかった場合
    return L"";
}

// プロセスを起動する関数
bool LaunchProcess(const std::string& applicationPathUtf8, const std::string& commandLineUtf8)
{
    std::wstring applicationPathW = FindExecutableInPath(Utf8ToUtf16(applicationPathUtf8));
    std::wstring commandLineW = Utf8ToUtf16(commandLineUtf8);

    // commandLine は書き換えられるので、バッファとして配列にコピー
    std::vector<wchar_t> cmdBuffer(commandLineW.begin(), commandLineW.end());
    cmdBuffer.push_back(L'\0');

    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi;

    BOOL success = CreateProcessW(
        applicationPathW.c_str(), // 実行ファイルのパス
        cmdBuffer.data(),         // コマンドライン（可変バッファ）
        NULL, NULL, FALSE,
        0, NULL, NULL, &si, &pi
    );

    if (success)
    {
        // オプション: プロセスの終了を待つ
        WaitForSingleObject(pi.hProcess, INFINITE);

        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        return true;
    }
    else
    {
        DWORD errorCode = GetLastError();
        std::wcerr << L"CreateProcessW failed. ErrorCode: " << errorCode << std::endl;
        std::wcerr << L"Message: " << GetLastErrorMessage(errorCode) << std::endl;
        return false;
    }
}


int main()
{
    CmdLauncher::Runner runner;
	runner.Run("config.yaml");

    std::string appPath = "fzf";
    std::string cmdLine = "";

    for (int i = 0; i < 2; i++) {
        if (!LaunchProcess(appPath, cmdLine))
        {
            std::cerr << "プロセスの起動に失敗しました。" << std::endl;
        }
    }

    return 0;

}
