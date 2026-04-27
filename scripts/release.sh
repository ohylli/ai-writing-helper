#!/usr/bin/env bash
# Build the release artifacts and create a draft GitHub release.
#
# Creates a DRAFT release so you can review it on GitHub before publishing.
# Publish via the web UI, or:  gh release edit <version> --draft=false
#
# Usage: ./scripts/release.sh v1.0.0

set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <version>" >&2
  echo "Example: $0 v1.0.0" >&2
  exit 1
fi

VERSION="$1"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

NOTES="RELEASE_NOTES.md"
if [[ ! -f "$NOTES" ]]; then
  echo "Error: $NOTES not found in $REPO_ROOT" >&2
  exit 1
fi

# build.sh validates the version format, so we don't repeat that here.
"$SCRIPT_DIR/build.sh" "$VERSION"

SC_OUT="dist/AIWritingHelper-${VERSION}-self-contained.exe"
FD_OUT="dist/AIWritingHelper-${VERSION}-framework-dependent.exe"

echo
echo "Creating draft GitHub release $VERSION..."
gh release create "$VERSION" \
  "$SC_OUT" \
  "$FD_OUT" \
  --title "$VERSION" \
  --notes-file "$NOTES" \
  --draft

echo
echo "Draft release created. Review it on GitHub, then publish via the web UI or:"
echo "  gh release edit $VERSION --draft=false"
