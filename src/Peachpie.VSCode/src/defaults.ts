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
    "Peachpie.App": "*"
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