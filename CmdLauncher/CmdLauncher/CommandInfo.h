#pragma once

#include <string>

#include <boost/algorithm/string/trim.hpp>
#include <boost/algorithm/string/split.hpp>

namespace CmdLauncher {
	class CommandInfo
	{
	public:
		static std::shared_ptr<CommandInfo> Create(const std::string& stmt)
		{
			auto commandInfo = std::make_shared<CommandInfo>();
			if (commandInfo->Parse(stmt) != 0) {
				return nullptr; // パースエラー
			}
			return commandInfo;
		}

		CommandInfo() = default;

		int Parse(const std::string& stmt)
		{
			// stmt には "\npower shell\npowershell\n-Command Get-Location" のような文字列を期待していて
			// それをパースして name, exec, args に分割する処理を実装する必要があります。
			// なので、stmt文字列をトリミングしたのち、改行で分割してそれぞれを name, exec, args に格納します。
			std::string trimmed = boost::algorithm::trim_copy(stmt);

			std::vector<std::string> result;
			boost::algorithm::split(result, trimmed, boost::is_any_of("\n"));
			if (result.size() < 3) {
				return -1; // パースエラー
			}

			name = boost::algorithm::trim_copy(result[0]);
			exec = boost::algorithm::trim_copy(result[1]);
			args = boost::algorithm::trim_copy(result[2]);

			return 0;
		}

		const std::string& GetName() const { return name; }

		const std::string& GetExec() const { return exec; }

		const std::string& GetArgs() const { return args; }

	private:
		std::string name;
		std::string exec;
		std::string args;
	};
}