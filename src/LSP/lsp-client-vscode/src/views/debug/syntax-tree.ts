import * as vscode from 'vscode';
import * as lc from "vscode-languageclient";
import { LanguageClient } from 'vscode-languageclient/node';

export const getSyntaxTreeRequest = new lc.RequestType<
	lc.TextDocumentPositionParams,
	SyntaxTreeNode | null,
	void
>("surab/getSyntaxTree");

export class SyntaxTreeNode {
	identifier: string | null;
	kind: string;
	span: vscode.Range;
	spanLabel: string;
	children: SyntaxTreeNode[];
}

export class SyntaxTreeProvider implements vscode.TreeDataProvider<SyntaxTreeNode> {
	private root: SyntaxTreeNode | null = null;

	private _onDidChangeTreeData: vscode.EventEmitter<SyntaxTreeNode | undefined | void> = new vscode.EventEmitter<SyntaxTreeNode | undefined | void>();
	readonly onDidChangeTreeData: vscode.Event<SyntaxTreeNode | undefined | void> = this._onDidChangeTreeData.event;

	constructor(
		public readonly workspaceFolder: vscode.WorkspaceFolder,
		private readonly client: LanguageClient) {
		this.registerEvents();
	}

	refresh(): void {
		this._onDidChangeTreeData.fire();
	}

	getTreeItem(node: SyntaxTreeNode): vscode.TreeItem | Promise<vscode.TreeItem> {
		var identifierText = node.identifier ? `@ ${node.identifier}` : "";
		const label = `${node.kind} ${identifierText} ${node.spanLabel}`;

		const item = new vscode.TreeItem(label, vscode.TreeItemCollapsibleState.Expanded);

		const icon = getIconFromNodeKind(node.kind);
		item.iconPath = icon ? new vscode.ThemeIcon(icon) : null;

		item.command = {
			title: "Open File",
			command: "vscode.open",
			arguments: [vscode.window.activeTextEditor.document.uri, {
				preserveFocus: true,
				selection: node.span,
			} as vscode.TextDocumentShowOptions],
		};

		return item;
	}

	async getChildren(node?: SyntaxTreeNode): Promise<SyntaxTreeNode[]> {
		if (node) {
			return node.children;
		}

		this.root = await this.sendRequest();
		return this.root.children;
	}

	// TODO:
	// getParent
	// resolveTreeItem

	private registerEvents() {
		vscode.window.onDidChangeActiveTextEditor(() => this.refresh());
		vscode.workspace.onDidChangeTextDocument(() => this.refresh());
	}

	private async sendRequest(): Promise<SyntaxTreeNode> {
		const editor = vscode.window.activeTextEditor;
		if (!editor) return;

		const textDocument = this.client.code2ProtocolConverter.asTextDocumentIdentifier(editor.document);
		const response = await this.client.sendRequest(getSyntaxTreeRequest, {
			textDocument,
			position: new vscode.Position(0, 0), // not used anyway, consider removing it
		});
		return response;
	}
}

function getIconFromNodeKind(kind: string): string | null {
	switch (kind) {
		case 'File': return 'symbol-file';
		case 'ImportsList': return 'symbol-module';
		case 'Import': return 'symbol-module';
		case 'ConstDecl': return 'symbol-constant';
		case 'VarDecl': return 'symbol-variable';
		case 'FnDecl': return 'symbol-function';
		case 'MethodDecl': return 'symbol-function';
		case 'AliasDecl': return 'symbol-key';
		case 'StructDecl': return 'symbol-struct';
		case 'TraitDecl': return 'symbol-interface';

		default: return null;
	}
}
