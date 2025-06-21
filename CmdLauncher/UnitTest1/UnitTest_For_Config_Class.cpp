#include "pch.h"
#include "CppUnitTest.h"
#include "../CmdLauncher/Config.h"
#include "../CmdLauncher/CommandInfo.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTest1
{
	TEST_CLASS(UnitTest_For_Config_Class)
	{
	public:
		TEST_METHOD(Version_ShouldBe_0_When_NoVersionSpecified)
		{
			const std::string yamlContents = R"()";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(0, config->GetVersion());
		}

		TEST_METHOD(Version_ShouldBe_1)
		{
			const std::string yamlContents = R"(version: 1)";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(1, config->GetVersion());
		}

		TEST_METHOD(Version_ShouldBe_100)
		{
			const std::string yamlContents = R"(version: 100)";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(100, config->GetVersion());
		}

		TEST_METHOD(List_ShouldBe_Empty_When_NoListSpecified)
		{
			const std::string yamlContents = R"()";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::IsTrue(config->GetList().empty());
		}

		TEST_METHOD(List_ShouldContain_OneItem)
		{
			const std::string yamlContents = R"(
list: 
  - |-
    name1
    exec1
    args1
)";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(size_t(1), config->GetList().size());
			Assert::AreEqual(std::string("name1"), config->GetList()[0].GetName());
			Assert::AreEqual(std::string("exec1"), config->GetList()[0].GetExec());
			Assert::AreEqual(std::string("args1"), config->GetList()[0].GetArgs());
		}

		TEST_METHOD(List_ShouldContain_TwoItems)
		{
			const std::string yamlContents = R"(
list: 
  - |-
    name1
    exec1
    args1
  - |-
    name2
    exec2
    args2
)";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(size_t(2), config->GetList().size());
			Assert::AreEqual(std::string("name1"), config->GetList()[0].GetName());
			Assert::AreEqual(std::string("exec1"), config->GetList()[0].GetExec());
			Assert::AreEqual(std::string("args1"), config->GetList()[0].GetArgs());
			Assert::AreEqual(std::string("name2"), config->GetList()[1].GetName());
			Assert::AreEqual(std::string("exec2"), config->GetList()[1].GetExec());
			Assert::AreEqual(std::string("args2"), config->GetList()[1].GetArgs());
		}

		TEST_METHOD(Alias_ShouldBe_Empty_When_NoAliasSpecified)
		{
			const std::string yamlContents = R"()";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::IsTrue(config->GetAlias().empty());
		}

		TEST_METHOD(Alias_ShouldContain_OneItem)
		{
			const std::string yamlContents = R"(alias: {key1: value1})";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(size_t(1), config->GetAlias().size());
			Assert::AreEqual(std::string("value1"), config->GetAlias().at("key1"));
		}

		TEST_METHOD(Alias_ShouldContain_TwoItems)
		{
			const std::string yamlContents = R"(alias: {key1: value1, key2: value2})";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(size_t(2), config->GetAlias().size());
			Assert::AreEqual(std::string("value1"), config->GetAlias().at("key1"));
			Assert::AreEqual(std::string("value2"), config->GetAlias().at("key2"));
		}

		TEST_METHOD(Bindings_ShouldBe_Empty_When_NoBindingsSpecified)
		{
			const std::string yamlContents = R"()";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::IsTrue(config->GetBindings().empty());
		}

		TEST_METHOD(Bindings_ShouldContain_OneItem)
		{
			const std::string yamlContents = R"(bindings: [binding1])";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(size_t(1), config->GetBindings().size());
			Assert::AreEqual(std::string("binding1"), config->GetBindings()[0]);
		}

		TEST_METHOD(Bindings_ShouldContain_TwoItems)
		{
			const std::string yamlContents = R"(bindings: [binding1, binding2])";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(size_t(2), config->GetBindings().size());
			Assert::AreEqual(std::string("binding1"), config->GetBindings()[0]);
			Assert::AreEqual(std::string("binding2"), config->GetBindings()[1]);
		}

		TEST_METHOD(ComplexConfig_ShouldWork)
		{
			const std::string yamlContents = R"(
version: 2
list: 
  - |-
    name1
    exec1
    args1
  - |-
    name2
    exec2
    args2
alias: 
  key1: value1
  key2: value2
bindings: [binding1, binding2]
)";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(2, config->GetVersion());
			Assert::AreEqual(size_t(2), config->GetList().size());
			Assert::AreEqual(std::string("name1"), config->GetList()[0].GetName());
			Assert::AreEqual(std::string("exec1"), config->GetList()[0].GetExec());
			Assert::AreEqual(std::string("args1"), config->GetList()[0].GetArgs());
			Assert::AreEqual(std::string("name2"), config->GetList()[1].GetName());
			Assert::AreEqual(std::string("exec2"), config->GetList()[1].GetExec());
			Assert::AreEqual(std::string("args2"), config->GetList()[1].GetArgs());
			Assert::AreEqual(size_t(2), config->GetAlias().size());
			Assert::AreEqual(std::string("value1"), config->GetAlias().at("key1"));
			Assert::AreEqual(std::string("value2"), config->GetAlias().at("key2"));
			Assert::AreEqual(size_t(2), config->GetBindings().size());
			Assert::AreEqual(std::string("binding1"), config->GetBindings()[0]);
			Assert::AreEqual(std::string("binding2"), config->GetBindings()[1]);
		}

		TEST_METHOD(EmptyConfig_ShouldWork)
		{
			const std::string yamlContents = R"()";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(0, config->GetVersion());
			Assert::IsTrue(config->GetList().empty());
			Assert::IsTrue(config->GetAlias().empty());
			Assert::IsTrue(config->GetBindings().empty());
		}

		TEST_METHOD(InvalidConfig_ShouldWork)
		{
			const std::string yamlContents = R"(version: not_a_number)";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(!errmsg.empty());
			Assert::IsTrue(config == nullptr); // Invalid config should return nullptr
		}

		TEST_METHOD(MissingFields_ShouldWork)
		{
			const std::string yamlContents = R"(version: 1
list: 
  - |-
    name1
    exec1
    args1
  - |-
    name2
    exec2
    args2
alias: 
  key1: value1
bindings: [binding1, binding2]
)";
			auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
			Assert::IsTrue(errmsg.empty());
			Assert::AreEqual(1, config->GetVersion());
			Assert::AreEqual(size_t(2), config->GetList().size());
			Assert::AreEqual(std::string("name1"), config->GetList()[0].GetName());
			Assert::AreEqual(std::string("exec1"), config->GetList()[0].GetExec());
			Assert::AreEqual(std::string("args1"), config->GetList()[0].GetArgs());
			Assert::AreEqual(std::string("name2"), config->GetList()[1].GetName());
			Assert::AreEqual(std::string("exec2"), config->GetList()[1].GetExec());
			Assert::AreEqual(std::string("args2"), config->GetList()[1].GetArgs());
			Assert::AreEqual(size_t(1), config->GetAlias().size());
			Assert::AreEqual(std::string("value1"), config->GetAlias().at("key1"));
			Assert::AreEqual(size_t(2), config->GetBindings().size());
			Assert::AreEqual(std::string("binding1"), config->GetBindings()[0]);

			// The missing fields should not cause any issues, they should just be empty
			Assert::IsTrue(config->GetAlias().find("key2") == config->GetAlias().end());
		}
	};
}
