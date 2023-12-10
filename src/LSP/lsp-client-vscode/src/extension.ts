import { ExtensionContext } from "vscode";

import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

let client: LanguageClient;

console.log("[STARTED]");

export function activate(context: ExtensionContext) {
  const serverExe = context.asAbsolutePath(
    "../lsp-server/bin/Debug/net8.0/surab-lsp-server.exe"
  );

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
  client.start();
}

export function deactivate(): Thenable<void> | undefined {
  if (!client) {
    return undefined;
  }
  return client.stop();
}
