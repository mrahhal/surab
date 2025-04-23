namespace Surab.Compilation;

// Ensure these match the corresponding values in scpp.
internal enum CallConv
{
	C = 0,
	Fast = 8,
	Cold = 9,
	X86_StdCall = 64,
	X86_FastCall = 65,
	X86_ThisCall = 70,
	Win64 = 79,
	X86_VectorCall = 80,
}
