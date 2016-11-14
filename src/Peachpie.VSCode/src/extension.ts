'use strict';
// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';

import { defaultProjectJson } from './defaults';

// this method is called when your extension is activated
// your extension is activated the very first time the command is executed
export function activate(context: vscode.ExtensionContext) {

    // Use the console to output diagnostic information (console.log) and errors (console.error)
    // This line of code will only be executed once when your extension is activated
    console.log('Congratulations, your extension "peachpie-vscode" is now active!');

    // The command has been defined in the package.json file
    // Now provide the implementation of the command with  registerCommand
    // The commandId parameter must match the command field in package.json
    let disposable = vscode.commands.registerCommand('peachpie.createProject', async () => {
        let rootPath = vscode.workspace.rootPath;
        if (rootPath == null) {
            vscode.window.showErrorMessage("A folder must be opened in Explorer panel");
        }

        let projectJsonUri = vscode.Uri.parse(`untitled:${rootPath}\\project.json`);
        let projectJsonDocument = await vscode.workspace.openTextDocument(projectJsonUri);
        let projectJsonContent = JSON.stringify(defaultProjectJson, null, 4);
        let projectJsonEdit = vscode.TextEdit.insert(new vscode.Position(0, 0), projectJsonContent);
    
        let wsEdit = new vscode.WorkspaceEdit();
        wsEdit.set(projectJsonUri, [ projectJsonEdit ]);
        let success = await vscode.workspace.applyEdit(wsEdit);
        
        success = success && await vscode.workspace.saveAll(true);

        if (success) {
            vscode.window.showInformationMessage("Peachpie PHP project was successfully created");
        } else {
            vscode.window.showErrorMessage("Error in creating Peachpie PHP project");
        }
    });

    context.subscriptions.push(disposable);
}

// this method is called when your extension is deactivated
export function deactivate() {
}