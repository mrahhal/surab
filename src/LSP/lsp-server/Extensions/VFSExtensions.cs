using OmniSharp.Extensions.LanguageServer.Protocol;
using Surab.Analysis;

namespace Surab.LSPServer;

public static class VFSExtensions
{
	public static ProjectFile? GetFile(this VFS vfs, DocumentUri uri)
	{
		var filePath = uri.GetFileSystemPath();
		return vfs.GetFile(filePath);
	}
}
