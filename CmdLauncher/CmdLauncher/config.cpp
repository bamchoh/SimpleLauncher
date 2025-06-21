
#include "Config.h"

namespace CmdLauncher
{
	std::tuple<std::string, std::shared_ptr<Config>> Config::Load(const std::string& yamlContents)
	{
		YAML::Node yamlConfig = YAML::Load(yamlContents);

		Config config;

		auto errmsg = config.Load(yamlConfig);
		if (!errmsg.empty()) {
			return { errmsg, nullptr };
		}

		return { "", std::make_shared<Config>(config)};
	}

	std::string Config::Load(YAML::Node& config)
	{
		try {
			if (config["version"]) {
				version = config["version"].as<int>();
			}

			if (config["list"]) {
				for (const auto& item : config["list"]) {
					list.push_back(item.as<std::string>());
				}
			}

			if (config["alias"]) {
				for (const auto& item : config["alias"]) {
					alias[item.first.as<std::string>()] = item.second.as<std::string>();
				}
			}

			if (config["bindings"]) {
				for (const auto& item : config["bindings"]) {
					bindings.push_back(item.as<std::string>());
				}
			}
		}
		catch (const YAML::Exception& e) {
			return "YAML parsing error: " + std::string(e.what());
		}
		catch (const std::exception& e) {
			return "Error: " + std::string(e.what());
		}

		return "";
	}
}