#!/usr/bin/env bash
# Build self-contained and framework-dependent single-file executables for a release.
# Output: dist/AIWritingHelper-<version>-self-contained.exe
#         dist/AIWritingHelper-<version>-framework-dependent.exe
#
# Usage: ./scripts/build.sh v1.0.0

set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <version>" >&2
  echo "Example: $0 v1.0.0" >&2
  exit 1
fi

VERSION="$1"

if [[ ! "$VERSION" =~ ^v[0-9]+\.[0-9]+\.[0-9]+(-.+)?$ ]]; then
  echo "Error: version must look like v1.2.3 or v1.2.3-beta (got: $VERSION)" >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

DIST="$REPO_ROOT/dist"
PROJECT="src/AIWritingHelper"

echo "Cleaning $DIST..."
rm -rf "$DIST"
mkdir -p "$DIST"

echo
echo "Building self-contained..."
dotnet publish "$PROJECT" \
  -c Release \
  --self-contained \
  -p:PublishSingleFile=true \
  -o "$DIST/self-contained"

echo
echo "Building framework-dependent..."
dotnet publish "$PROJECT" \
  -c Release \
  --no-self-contained \
  -r win-x64 \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=false \
  -o "$DIST/framework-dependent"

SC_OUT="$DIST/AIWritingHelper-${VERSION}-self-contained.exe"
FD_OUT="$DIST/AIWritingHelper-${VERSION}-framework-dependent.exe"

cp "$DIST/self-contained/AIWritingHelper.exe"      "$SC_OUT"
cp "$DIST/framework-dependent/AIWritingHelper.exe" "$FD_OUT"

echo
echo "Build complete:"
ls -lh "$SC_OUT" "$FD_OUT" | awk '{ printf "  %-12s  %s\n", $5, $NF }'
