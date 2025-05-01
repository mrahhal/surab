using System.Runtime.InteropServices;
using System.Text;

namespace Surab.Compilation.LLVM;

internal unsafe static class SCPPExtensions
{
	public static string AsString(sbyte* p)
	{
		var span = MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)p);
		return span.AsString();
	}

	public static string AsString(this Span<byte> span) => AsString((ReadOnlySpan<byte>)span);

	public static string AsString(this ReadOnlySpan<byte> span)
	{
		if (span.IsEmpty)
		{
			return string.Empty;
		}

		fixed (byte* pSelf = span)
		{
			return Encoding.UTF8.GetString(pSelf, span.Length);
		}
	}

	public static string AsString(this ReadOnlySpan<ushort> span)
	{
		if (span.IsEmpty)
		{
			return string.Empty;
		}

		fixed (ushort* pSelf = span)
		{
			return Encoding.Unicode.GetString((byte*)pSelf, span.Length * 2);
		}
	}

	public static string AsString(this ReadOnlySpan<uint> span)
	{
		if (span.IsEmpty)
		{
			return string.Empty;
		}

		fixed (uint* pSelf = span)
		{
			return Encoding.UTF32.GetString((byte*)pSelf, span.Length * 4);
		}
	}
}
