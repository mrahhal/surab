# lsp-client-vscode

https://code.visualstudio.com/api/working-with-extensions/publishing-extension

Ensure vsce is installed:

```
npm install -g @vscode/vsce
```

Package into a VSIX to be installed manually from vscode:

```
vsce package
```

Install:

```
code --install-extension surab-lsp-client-vscode-0.0.1.vsix
```
