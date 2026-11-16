#!/usr/bin/env bash
# Generates .gen.cs files from every .tpl.cs file under the native iOS Controls folder
# (Bruinet.NativeIos) using the Bruinet ControlObject generator.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLI_PROJECT="$SCRIPT_DIR/../Bruinet.Cli/Bruinet.Cli.csproj"
CLI_DLL="$SCRIPT_DIR/../Bruinet.Cli/bin/Release/net10.0/bruinet.dll"
INPUT="$SCRIPT_DIR/../../src/Bruinet.NativeIos/Controls"

echo "Building bruinet..."
dotnet build "$CLI_PROJECT" -c Release -v:minimal /nr:false

echo "Running generator..."
dotnet "$CLI_DLL" controls generate "$INPUT"

echo "Done. Generated output in: $INPUT"
