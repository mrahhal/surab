#include "surab_clang.h"

#include <vector>
#include <string>
#include <sstream>

#include "clang/Driver/Compilation.h"
#include "clang/Driver/Driver.h"
#include "clang/Frontend/TextDiagnosticPrinter.h"
#include "llvm/Support/raw_os_ostream.h"
#include "llvm/Support/VirtualFileSystem.h"

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

#if defined(_WIN32)
// TODO: Does this mean lld-link and thus llvm is required on windows? What if we remove this?
#define FUSE_LD "-fuse-ld=lld-link"
#else
#define FUSE_LD "-fuse-ld=ld"
#endif

int scpp_clang(const char *triple, const char *additionalLibs, const char *inputFiles, const char *outputFile, const char **outError)
{
	// TODO: Use correct fuse-ld based on platform.

	std::vector<const char *> args = {
		"clang",
		FUSE_LD,
		"-fPIE"
	};

	args.push_back("-o");
	args.push_back(outputFile);

	for (auto &additionalLib : split_string(additionalLibs, ';'))
	{
		auto libArg = "-l" + additionalLib;
		args.push_back(strdup(libArg.c_str()));
	}

	for (auto &inputFile : split_string(inputFiles, ';'))
	{
		args.push_back(strdup(inputFile.c_str()));
	}

	clang::IntrusiveRefCntPtr<clang::DiagnosticOptions> diagOpts = new clang::DiagnosticOptions();
	clang::TextDiagnosticPrinter *diagPrinter =
		new clang::TextDiagnosticPrinter(llvm::errs(), diagOpts.get());
	clang::IntrusiveRefCntPtr<clang::DiagnosticIDs> diagID(new clang::DiagnosticIDs());
	clang::DiagnosticsEngine diags(diagID, diagOpts, diagPrinter);

	clang::driver::Driver driver("clang", triple, diags);
	auto compilation = driver.BuildCompilation(args);

	auto &jobs = compilation->getJobs();
	for (const auto &job : jobs)
	{
		llvm::errs() << "Job: " << job.getExecutable() << "\n";
		for (const auto &arg : job.getArguments())
			llvm::errs() << "  Arg: " << arg << "\n";
	}

	llvm::SmallVector<std::pair<int, const clang::driver::Command *>, 4> results;
	auto ret = driver.ExecuteCompilation(*compilation, results);

	if (ret != 0)
	{
		llvm::errs() << "Compilation failed with exit code " << ret << "\n";
	}
	else
	{
		llvm::outs() << "Compilation succeeded!\n";
	}

	return ret;
}
