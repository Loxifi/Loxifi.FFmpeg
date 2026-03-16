#!/bin/bash
# ============================================================================
# publish.sh — Version, test, pack, and publish all NuGet packages
# ============================================================================
#
# Bumps the semantic version, runs the full test suite, packs all NuGet
# packages with the new version, pushes them to nuget.org, then commits
# the version bump and creates a git tag.
#
# Usage:
#   ./publish.sh [major|minor|patch]
#
# Arguments:
#   $1  Version bump type. Default: patch
#
# Prerequisites:
#   - .NET 9 SDK
#   - NUGET_API_KEY environment variable set to a valid NuGet API key
#   - All tests must pass (test.sh is run automatically)
#   - Clean git working tree recommended
#
# Environment variables:
#   NUGET_API_KEY  (required) API key for pushing to nuget.org
# ============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
VERSION_FILE="$SCRIPT_DIR/VERSION"
NUGET_KEY="${NUGET_API_KEY:?Set NUGET_API_KEY environment variable}"
NUGET_SOURCE="https://api.nuget.org/v3/index.json"
RELEASE_DIR="$SCRIPT_DIR/release"

# ── Version management ────────────────────────────────────────────────────────
# Reads the current version from VERSION file, bumps it according to the
# requested level (major/minor/patch), and computes the new version string.

if [ ! -f "$VERSION_FILE" ]; then
    echo "1.0.0" > "$VERSION_FILE"
fi

CURRENT_VERSION=$(cat "$VERSION_FILE")
echo "Current version: $CURRENT_VERSION"

# Parse semver components
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"

BUMP="${1:-patch}"
case "$BUMP" in
    major) MAJOR=$((MAJOR + 1)); MINOR=0; PATCH=0 ;;
    minor) MINOR=$((MINOR + 1)); PATCH=0 ;;
    patch) PATCH=$((PATCH + 1)) ;;
    *)     echo "Usage: $0 [major|minor|patch]"; exit 1 ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"
echo "New version: $NEW_VERSION"

# ── Run all tests ─────────────────────────────────────────────────────────────
# Ensures the full test suite passes before publishing to nuget.org.

echo ""
echo "========================================"
echo "  Running all tests before publish"
echo "========================================"

"$SCRIPT_DIR/test.sh"

echo ""
echo "All tests passed."

# ── Build and pack ────────────────────────────────────────────────────────────
# Packs all projects (core library + all runtime packages) with the new version.

echo ""
echo "========================================"
echo "  Building and packing v$NEW_VERSION"
echo "========================================"

rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

PROJECTS=(
    "src/Loxifi.FFmpeg/Loxifi.FFmpeg.csproj"
    "src/Loxifi.FFmpeg.Runtime.linux-x64/Loxifi.FFmpeg.Runtime.linux-x64.csproj"
    "src/Loxifi.FFmpeg.Runtime.win-x64/Loxifi.FFmpeg.Runtime.win-x64.csproj"
    "src/Loxifi.FFmpeg.Runtime.android-arm64/Loxifi.FFmpeg.Runtime.android-arm64.csproj"
    "src/Loxifi.FFmpeg.Runtime.linux-x64.GPL/Loxifi.FFmpeg.Runtime.linux-x64.GPL.csproj"
    "src/Loxifi.FFmpeg.Runtime.win-x64.GPL/Loxifi.FFmpeg.Runtime.win-x64.GPL.csproj"
    "src/Loxifi.FFmpeg.Runtime.android-arm64.GPL/Loxifi.FFmpeg.Runtime.android-arm64.GPL.csproj"
)

for proj in "${PROJECTS[@]}"; do
    echo "Packing $proj..."
    dotnet pack "$SCRIPT_DIR/$proj" \
        -c Release \
        -o "$RELEASE_DIR" \
        -p:PackageVersion="$NEW_VERSION" \
        --nologo
done

echo ""
echo "Packages:"
ls -1 "$RELEASE_DIR"/*.nupkg

# ── Push to NuGet ─────────────────────────────────────────────────────────────
# Pushes each .nupkg to nuget.org. --skip-duplicate avoids errors if a package
# version was already published (e.g. from a partial previous run).

echo ""
echo "========================================"
echo "  Pushing to NuGet"
echo "========================================"

for pkg in "$RELEASE_DIR"/*.nupkg; do
    echo "Pushing $(basename "$pkg")..."
    dotnet nuget push "$pkg" \
        --api-key "$NUGET_KEY" \
        --source "$NUGET_SOURCE" \
        --skip-duplicate
done

# ── Update version file, commit, and tag ──────────────────────────────────────

echo "$NEW_VERSION" > "$VERSION_FILE"

git add "$VERSION_FILE"
git commit -m "Bump version to $NEW_VERSION

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
git tag "v$NEW_VERSION"
git push && git push --tags

echo ""
echo "========================================"
echo "  Published v$NEW_VERSION to NuGet"
echo "========================================"
