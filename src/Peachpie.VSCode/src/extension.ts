'use strict';
// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';

import * as cp from 'child_process';

import { defaultProjectJson, defaultTasksJson, defaultLaunchJson } from './defaults';

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

        let isProjectJsonSuccess = await createProjectJson(rootPath);
        if (isProjectJsonSuccess) {
            vscode.window.showInformationMessage(".NET Core project.json configuration file was successfully created");
        } else {
            vscode.window.showErrorMessage("Error in creating .NET Core project.json configuration file");
            return;
        }

        let isTasksSuccess = (await configureTasks()) && (await configureLaunch());
        if (isTasksSuccess) {
            vscode.window.showInformationMessage("Build tasks successfully configured");
        } else {
            vscode.window.showErrorMessage("Error in configuring the build tasks");
        }

        await execChildProcess("dotnet restore", rootPath)
        .catch((error) => {
            vscode.window.showErrorMessage("For building and executing, Peachpie needs .NET Core CLI tools to be available on the path. Make sure they are installed properly.");
        }).then((data: string) => {
            if (data.includes("Restore completed in")) {
                vscode.window.showInformationMessage("Project dependencies were successfully installed");
            } else {
                vscode.window.showErrorMessage("Error in installing project dependencies");
            }
        });

        let csharpExtension = vscode.extensions.getExtension("ms-vscode.csharp");
        if (csharpExtension == null) {
            vscode.window.showErrorMessage("Install C# extension powered by Omnisharp in order to enable the debugging of Peachpie projects");            
        } else {
            csharpExtension.activate();
        }
    });

    context.subscriptions.push(disposable);
}

// Create project.json file in the opened root folder
async function createProjectJson(rootPath: string): Promise<boolean> {
    let projectJsonUri = vscode.Uri.parse(`untitled:${rootPath}\\project.json`);
    let projectJsonDocument = await vscode.workspace.openTextDocument(projectJsonUri);
    let projectJsonContent = JSON.stringify(defaultProjectJson, null, 4);
    let projectJsonEdit = vscode.TextEdit.insert(new vscode.Position(0, 0), projectJsonContent);
    
    let wsEdit = new vscode.WorkspaceEdit();
    wsEdit.set(projectJsonUri, [ projectJsonEdit ]);
    let isSuccess = await vscode.workspace.applyEdit(wsEdit);

    if (isSuccess) {
        isSuccess = await vscode.workspace.saveAll(true);
    }

    return isSuccess;
}

// Overwrite tasks configuration, resulting in adding or replacing .vscode/tasks.json
async function configureTasks(): Promise<boolean> {
    return overwriteConfiguration("tasks", defaultTasksJson);
}

// Overwrite launch configuration, resulting in adding or replacing .vscode/tasks.json
async function configureLaunch(): Promise<boolean> {
    return overwriteConfiguration("launch", defaultLaunchJson);
}

async function overwriteConfiguration(section: string, configuration: any): Promise<boolean> {
    let tasksConfig = vscode.workspace.getConfiguration(section);
    if (tasksConfig == null) {
        console.error(`Unable to load ${section} configuration`);
        return false;
    }

    try {
        for (var key in configuration) {
            if (configuration.hasOwnProperty(key)) {
                var element = configuration[key];

                // Not defined in the Typescript interface, therefore called this way
                await tasksConfig['update'].call(tasksConfig, key, element);
            }
        }
    } catch (error) {
        console.error("Error in configuring the build tasks: %s", (<Error>error).message);
        return false;
    }

    return true;
}

// Taken from omnisharp-vscode
function execChildProcess(command: string, workingDirectory: string): Promise<string> {
    return new Promise<string>((resolve, reject) => {
        cp.exec(command, { cwd: workingDirectory, maxBuffer: 500 * 1024 }, (error, stdout, stderr) => {
            if (error) {
                reject(error);
            }
            else if (stderr && stderr.length > 0) {
                reject(new Error(stderr));
            }
            else {
                resolve(stdout);
            }
        });
    });
}

// this method is called when your extension is deactivated
export function deactivate() {
}