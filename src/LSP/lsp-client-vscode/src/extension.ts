import * as vscode from "vscode";
import * as lc from "vscode-languageclient/node";
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
	const serverExe = context.asAbsolutePath("../lsp-server/bin/Debug/net9.0/surab-lsp-server.exe");

	const command = `${serverExe}`;

	const serverOptions: ServerOptions = {
		command,
		transport: TransportKind.pipe,
	};

	const clientOptions: LanguageClientOptions = {
		// documentSelector: [{ scheme: "file", language: "surab" }], ??
	};

	client = new LanguageClient(
		"surab-lsp-client",
		"Surab LSP Client",
		serverOptions,
		clientOptions
	);

	client.registerFeature(new ExperimentalFeatures());
	client.registerFeature(new OverrideFeatures());

	// Start the client. This will also launch the server.
	await client.start();

	// Only add these after server inits? And disable when disconnected?
	context.subscriptions.push(vscode.commands.registerCommand("surab-lsp-client-vscode.showImplementations", async (uri, position, locations) => {
		await vscode.commands.executeCommand(
			"editor.action.showReferences",
			vscode.Uri.parse(uri),
			client.protocol2CodeConverter.asPosition(position),
			locations.map(client.protocol2CodeConverter.asLocation),
		);
	}));

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

class ExperimentalFeatures implements lc.StaticFeature {
	constructor() { }

	getState(): lc.FeatureState {
		return { kind: "static" };
	}

	fillClientCapabilities(capabilities: lc.ClientCapabilities): void {
		capabilities.experimental = {
			commands: {
				commands: [
					"surab-lsp-client-vscode.showImplementations",
				],
			},
			...capabilities.experimental,
		};
	}

	initialize(
		_capabilities: lc.ServerCapabilities,
		_documentSelector: lc.DocumentSelector | undefined,
	): void { }

	dispose(): void { }

	clear(): void { }
}

class OverrideFeatures implements lc.StaticFeature {
	getState(): lc.FeatureState {
		return { kind: "static" };
	}

	fillClientCapabilities(capabilities: lc.ClientCapabilities): void {
		const semanticTokens = capabilities.textDocument?.semanticTokens;
		if (semanticTokens) {
			// Disable non semantic tokens for colorization for now.
			semanticTokens.augmentsSyntaxTokens = false;
		}
	}

	initialize(
		_capabilities: lc.ServerCapabilities,
		_documentSelector: lc.DocumentSelector | undefined,
	): void { }

	dispose(): void { }

	clear(): void { }
}
