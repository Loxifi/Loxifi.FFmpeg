#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
VERSION_FILE="$SCRIPT_DIR/VERSION"
NUGET_KEY="${NUGET_API_KEY:?Set NUGET_API_KEY environment variable}"
NUGET_SOURCE="https://api.nuget.org/v3/index.json"
RELEASE_DIR="$SCRIPT_DIR/release"

# ── Version management ──

if [ ! -f "$VERSION_FILE" ]; then
    echo "1.0.0" > "$VERSION_FILE"
fi

CURRENT_VERSION=$(cat "$VERSION_FILE")
echo "Current version: $CURRENT_VERSION"

# Parse semver
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

# ── Run all tests ──

echo ""
echo "========================================"
echo "  Running all tests before publish"
echo "========================================"

"$SCRIPT_DIR/test.sh"

echo ""
echo "All tests passed."

# ── Build and pack ──

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

# ── Push to NuGet ──

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

# ── Update version file and commit ──

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
