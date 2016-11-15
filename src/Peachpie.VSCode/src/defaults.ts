'use strict';

export var defaultProjectJson =
{
  "version": "1.0.0-*",
  "description": "PHP project compiled into .NET by Peachpie",
  "buildOptions": {
    "compilerName": "php",
    "compile": "**\\*.php",
    "emitEntryPoint": true,
    "debugType": "portable"
  },
  "dependencies": {
    "Peachpie.App": "0.3.0-*"
  },
  "tools": {
    "Peachpie.Compiler.Tools": "0.3.0-*"
  },
  "frameworks": {
    "netcoreapp1.0": {
      "dependencies": {
        "Microsoft.NETCore.App": {
          "type": "platform",
          "version": "1.0.0"
        }
      }
    }
  }
};

export var defaultTasksJson =
{
    "version": "0.1.0",
    "command": "dotnet",
    "isShellCommand": true,
    "args": [],
    "tasks": [
        {
            "taskName": "build",
            "args": [
                "${workspaceRoot}\\project.json"
            ],
            "isBuildCommand": true,
            "problemMatcher": "$msCompile"
        }
    ]
};