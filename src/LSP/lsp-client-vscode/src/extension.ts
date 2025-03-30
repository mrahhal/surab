import * as vscode from "vscode";
import {
	LanguageClient,
	LanguageClientOptions,
	ServerOptions,
	TransportKind,
} from "vscode-languageclient/node";
import { SyntaxTreeProvider } from "./views/debug/syntax-tree";

let client: LanguageClient;

console.log("[STARTED]");

export async function activate(context: vscode.ExtensionContext) {
	// const serverExe = context.asAbsolutePath(
	// 	"../lsp-server/bin/Debug/net8.0/surab-lsp-server.exe"
	// );

	// TODO:
	const serverExe = "C:/dev/src/Projects/surab/src/LSP/lsp-server/bin/Debug/net9.0/surab-lsp-server.exe";

	const command = `${serverExe}`;

	const serverOptions: ServerOptions = {
		command,
		transport: TransportKind.stdio,
	};

	const clientOptions: LanguageClientOptions = {};

	client = new LanguageClient(
		"surab-lsp-client",
		"Surab LSP Client",
		serverOptions,
		clientOptions
	);

	// Start the client. This will also launch the server.
	await client.start();

	const workspace =
		vscode.workspace.workspaceFolders && vscode.workspace.workspaceFolders.length > 0
			? vscode.workspace.workspaceFolders[0]
			: null;

	if (!workspace) {
		return;
	}

	const syntaxTreeProvider = new SyntaxTreeProvider(workspace, client);
	vscode.window.registerTreeDataProvider(
		'surab-debug-syntax-tree',
		syntaxTreeProvider,
	);
}

export function deactivate(): Thenable<void> | undefined {
	if (!client) {
		return undefined;
	}
	return client.stop();
}
