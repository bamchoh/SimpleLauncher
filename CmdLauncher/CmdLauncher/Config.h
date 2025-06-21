#pragma once

#include "yaml-cpp/yaml.h"

namespace CmdLauncher
{
	class Config
	{
	public:
		static std::tuple<std::string, std::shared_ptr<Config>> Load(const std::string& yamlContents);

		Config() {};

		std::string Load(YAML::Node& config);

		int GetVersion() const { return version; }

		const std::vector<std::string>& GetList() const { return list; }

		const std::map<std::string, std::string>& GetAlias() const { return alias; }

		const std::vector<std::string>& GetBindings() const { return bindings; }

	private:
		int version = 0;
		std::vector<std::string> list;
		std::map<std::string, std::string> alias;	
		std::vector<std::string> bindings;
	};
}


