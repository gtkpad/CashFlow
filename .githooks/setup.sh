#!/bin/bash
set -eu

git config core.hooksPath .githooks

echo "Git hooks configurados com sucesso!"
echo "O pre-commit ira:"
echo "  1. Formatar arquivos .cs automaticamente (dotnet format)"
echo "  2. Verificar lint via build (TreatWarningsAsErrors=true)"
