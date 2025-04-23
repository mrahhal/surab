#include "surab_lld.h"

#include <vector>
#include <string>
#include <sstream>

#include "llvm/Support/raw_os_ostream.h"
#include "lld/Common/Driver.h"

std::vector<std::string> split_string(const char *str, char delimiter)
{
	std::vector<std::string> result;
	std::istringstream stream(str);
	std::string token;

	while (std::getline(stream, token, delimiter))
	{
		result.push_back(token);
	}

	return result;
}

bool scpp_lld(const char *additionalLibs, const char *inputFiles, const char *outputFile, const char **outError)
{
	// TODO: This works only for windows. Use "ld.lld", "ld64.lld", or "lld-link" for ELF, Mach-O, or COFF respectively.
	// You can also pass --flavor (gnu, darwin, or link) to override detection.
	auto lldDriver = "lld-link";

	std::vector<const char *> args = {
		lldDriver,
		"-o",
		outputFile,
	};

	for (auto &additionalLib : split_string(additionalLibs, ';'))
	{
		auto libArg = "-l" + additionalLib;
		args.push_back(strdup(libArg.c_str()));
	}

	for (auto &ollFile : split_string(inputFiles, ';'))
	{
		args.push_back(strdup(ollFile.c_str()));
	}

	std::ostringstream errorStream;
	llvm::raw_os_ostream llvmErrStream(errorStream);
	llvm::ArrayRef<lld::DriverDef> drivers;

	auto result = lld::lldMain(args, llvm::outs(), llvmErrStream, drivers);

	if (!drivers.empty())
	{
		llvm::outs() << "LLD tried the following drivers: ";
		for (const auto &driver : drivers)
		{
			llvm::outs() << driver.f << " ";
		}
		llvm::outs() << "\n";
	}

	if (result.retCode != 0 && outError)
	{
		std::string errStr = errorStream.str();
		*outError = strdup(errStr.c_str()); // caller must free
	}

	return result.retCode ? true : false;
}
