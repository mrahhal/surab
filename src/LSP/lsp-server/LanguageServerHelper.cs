using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Surab.LSPServer;

public static class LanguageServerHelper
{
	public static TextDocumentSelector CreateSurabDocumentSelector()
	{
		return new(new TextDocumentFilter
		{
			Pattern = GetSurabFilesGlob(),
		});
	}

	public static string GetSurabFilesGlob() => "**/*.surab";
}
