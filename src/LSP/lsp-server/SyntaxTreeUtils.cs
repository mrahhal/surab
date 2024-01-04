using System.Text;
using Surab.Analysis;

namespace Surab.LSPServer;

public static class SyntaxTreeUtils
{
	public static string GetSigStringWithoutIdentifier(FnSigNode sig)
	{
		var sb = new StringBuilder();
		sb.Append(sig.ParamsList.ToString());
		sb.Append(sig.ReturnType == null ? string.Empty : $" {sig.ReturnType}");
		return sb.ToString().Trim();
	}
}
