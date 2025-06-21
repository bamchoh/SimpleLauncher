#include "Runner.h"

#include <fstream>
#include <iostream>

#include "Config.h"

namespace CmdLauncher {
	int Runner::Run(std::string configFilepath)
	{
		std::fstream file(configFilepath, std::ios::in);
		if (!file.is_open())
		{
			std::cerr << "Failed to open config file: " << configFilepath << std::endl;
			return 1;
		}

		std::string yamlContents((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());

		auto [errmsg, config] = CmdLauncher::Config::Load(yamlContents);
		if (!errmsg.empty())
		{
			std::cerr << "Error loading config: " << errmsg << std::endl;
			return 1;
		}
		std::cout << "Config loaded successfully." << std::endl;

		return 0;
	}
}

