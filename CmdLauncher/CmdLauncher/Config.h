#pragma once

#include "yaml-cpp/yaml.h"

#include "CommandInfo.h"

namespace CmdLauncher
{
	class Config
	{
	public:
		static std::tuple<std::string, std::shared_ptr<Config>> Load(const std::string& yamlContents);

		Config() {};

		std::string Load(YAML::Node& config);

		int GetVersion() const { return version; }

		const std::vector<CmdLauncher::CommandInfo>& GetList() const { return list; }

		const std::vector<std::string> GetNameList() const
		{
			std::vector<std::string> names;
			for (const auto& command : list) {
				names.push_back(command.GetName());
			}
			return names;
		}

		const std::map<std::string, std::string>& GetAlias() const { return alias; }

		const std::vector<std::string>& GetBindings() const { return bindings; }

	private:
		int version = 0;
		std::vector<CmdLauncher::CommandInfo> list;
		std::map<std::string, std::string> alias;	
		std::vector<std::string> bindings;
	};
}


