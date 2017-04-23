# Peachpie for Visual Studio Code

This is the official extension of Peachpie for Visual Studio Code. It integrates the PHP compiler to .NET into VSCode, automatically downloading the required dependencies and allowing for a comfortable development experience with Peachpie.

> Please note that Peachpie compiler is still a work in progress. Therefore, some functionalities are not yet supported. For an updated list of supported constructs, please see [our roadmap](https://github.com/iolevel/peachpie/wiki/Peachpie-Roadmap).

![Peachpie for VS Code](images/tEDLQt.gif)

## Features

* Adds 'Create Project' command directly in Visual Studio Code
* Automatically downloads Peachpie dependencies
* Automatically enables the C# extension, if already installed
* Enables breakpoints in .php files
* Underlining of syntax errors
* Peachpie Diagnostics underlining missing or not yet implemented functions

*'Create Project' with Peachpie* 
![Create Project Command](images/create-project.png)

*Adding breakpoints in .php files*
![Create Project Command](images/breakpoint.png)

*Debugging PHP in VSCode*
![Create Project Command](images/debug.png)

*Syntax error highlighting*
![Syntax error](images/syntax-error.png)

*Peachpie Diagnostics*
![Diagnostics](images/unresolved-diagnostics.png)

## Requirements

It is necessary to install the C# for Visual Studio Code extension first. Check out the extension in the [VSCode Marketplace](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp) and download it directly in VSCode by typing the command `ext install csharp`.

## Known Issues

Peachpie compiler is a work in progress, and thus many functionalities are not yet supported. Please see the project's [repository](https://www.github.com/iolevel/peachpie) for limitations, supported constructs and specifications.

## Release Notes

### 0.7.0
- Re-enabled peachpie project diagnostics
- New projects target Peachpie 0.7.0

### 0.6.0
- Support for new msbuild project format
- `project.json` deprecated and removed
- Updated `Create Project` command

### 0.5.0

- New project refers to Peachpie 0.5.0
- Underlining of syntax errors
- Code Analysis diagnostics

### 0.3.0

- Initial release

-----------------------------------------------------------------------------------------------------------

### For more information

For more information, please visit:
* [The project website](http://www.peachpie.io)
* [The GitHub repository](https://github.com/iolevel/peachpie)

**Enjoy!**
