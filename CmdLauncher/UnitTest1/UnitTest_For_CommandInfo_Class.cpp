#include "pch.h"
#include "CppUnitTest.h"
#include "../CmdLauncher/CommandInfo.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTest1
{
	TEST_CLASS(UnitTest_For_CommandInfo_Class)
	{
	public:
		TEST_METHOD(Parse_ShouldReturn_ValidCommandInfo)
		{
			std::string stmt = "power shell\npowershell\n-Command Get-Location";
			auto commandInfo = CmdLauncher::CommandInfo::Create(stmt);
			Assert::IsNotNull(commandInfo.get());
			Assert::AreEqual(std::string("power shell"), commandInfo->GetName());
			Assert::AreEqual(std::string("powershell"), commandInfo->GetExec());
			Assert::AreEqual(std::string("-Command Get-Location"), commandInfo->GetArgs());
		}

		TEST_METHOD(Parse_ShouldReturn_Null_WhenParsingFails)
		{
			std::string stmt = "invalid command";
			auto commandInfo = CmdLauncher::CommandInfo::Create(stmt);
			Assert::IsNull(commandInfo.get()); // パースエラーで nullptr が返ることを確認
		}

		TEST_METHOD(Parse_ShouldTrimWhitespace)
		{
			std::string stmt = "   power shell   \n   powershell   \n   -Command Get-Location   ";
			auto commandInfo = CmdLauncher::CommandInfo::Create(stmt);
			Assert::IsNotNull(commandInfo.get());
			Assert::AreEqual(std::string("power shell"), commandInfo->GetName());
			Assert::AreEqual(std::string("powershell"), commandInfo->GetExec());
			Assert::AreEqual(std::string("-Command Get-Location"), commandInfo->GetArgs());
		}

		TEST_METHOD(Parse_ShouldHandle_EmptyCommand)
		{
			std::string stmt = "\n\n";
			auto commandInfo = CmdLauncher::CommandInfo::Create(stmt);
			Assert::IsNull(commandInfo.get()); // 空のコマンドはパースエラー
		}

		TEST_METHOD(Parse_ShouldHandle_SingleLineCommand)
		{
			std::string stmt = "single command";
			auto commandInfo = CmdLauncher::CommandInfo::Create(stmt);
			Assert::IsNull(commandInfo.get()); // 改行がないとパースエラー
		}

		TEST_METHOD(Parse_ShouldReturn_ValidCommandInfo_EvenThough_There_are_4_lines)
		{
			std::string stmt = "power shell\npowershell\n-Command Get-Location\nextra line";
			auto commandInfo = CmdLauncher::CommandInfo::Create(stmt);
			Assert::IsNotNull(commandInfo.get());
			Assert::AreEqual(std::string("power shell"), commandInfo->GetName());
			Assert::AreEqual(std::string("powershell"), commandInfo->GetExec());
			Assert::AreEqual(std::string("-Command Get-Location"), commandInfo->GetArgs());
		}

		TEST_METHOD(Parse_ShouldReturn_ValidCommandInfo_EvenThough_JapaneseWords_Are_Included)
		{
			std::string stmt = "パワーシェル\npowershell\n-Command Get-Location";
			auto commandInfo = CmdLauncher::CommandInfo::Create(stmt);
			Assert::IsNotNull(commandInfo.get());
			Assert::AreEqual(std::string("パワーシェル"), commandInfo->GetName());
			Assert::AreEqual(std::string("powershell"), commandInfo->GetExec());
			Assert::AreEqual(std::string("-Command Get-Location"), commandInfo->GetArgs());
		}
	};
}