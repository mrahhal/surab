import * as vscode from "vscode";

export type SurabDocument = vscode.TextDocument & { languageId: "surab" };
export type SurabEditor = vscode.TextEditor & { document: SurabDocument };

export function isSurabDocument(document: vscode.TextDocument): document is SurabDocument {
	return document.languageId === "surab" && document.uri.scheme === "file";
}

export function isSurabEditor(editor: vscode.TextEditor): editor is SurabEditor {
	return isSurabDocument(editor.document);
}
