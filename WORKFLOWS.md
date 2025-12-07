# Build & Release Workflows

This document explains the automated build and release workflows for BarcodeRevealTool.

## Build Workflow (`.github/workflows/build.yml`)

Runs on every push and pull request to `main` and `develop` branches.

### What it does:
- Builds the project in both **Debug** and **Release** configurations
- Runs unit tests (Release configuration only)
- Uploads build artifacts for the Release configuration
- Runs on: **Windows Latest** (.NET 8.0)

### Artifacts:
- **build-release**: Compiled Release binaries (7-day retention)
- **test-results**: Test result files in TRX format (30-day retention)

### Configuration:
The workflow runs for both Debug and Release, but tests and artifacts are only generated for Release builds.

---

## Release Workflow (`.github/workflows/release.yml`)

Automatically triggered when you push a version tag.

### How to create a release:

```bash
# Locally, create and push a version tag
git tag v0.2.0
git push origin v0.2.0
```

Or using GitHub CLI:
```bash
gh release create v0.2.0 --generate-notes
```

### What it does:
1. **Builds** the solution in Release configuration
2. **Publishes** self-contained executables for multiple platforms:
   - **win-x64**: Intel/AMD 64-bit Windows (most users)
   - **win-arm64**: ARM64 Windows (Surface Pro, new devices)
3. **Packages** each platform as a ZIP file with:
   - Compiled executable and dependencies
   - README.md
   - ARCHITECTURE.md (if present)
4. **Creates a GitHub Release** with:
   - Both ZIP packages as downloadable artifacts
   - Automatic release notes
   - Correctly marked as pre-release for alpha/beta versions

### Version Tag Format:

Use semantic versioning:
- `v1.0.0` - Stable release
- `v0.2.0-alpha` or `v0.2.0-alpha.1` - Alpha release (marked as pre-release)
- `v0.2.0-beta` - Beta release (marked as pre-release)
- `v0.2.0-rc.1` - Release candidate

### Artifacts Generated:

For tag `v0.2.0`, you'll get:
- `BarcodeRevealTool-v0.2.0-win-x64.zip`
- `BarcodeRevealTool-v0.2.0-win-arm64.zip`

Each ZIP includes:
```
BarcodeRevealTool-v0.2.0-win-x64/
├── BarcodeRevealTool.exe
├── *.dll (all dependencies)
├── appsettings.json
├── README.md
└── ARCHITECTURE.md
```

---

## Quick Start

### For local development:
```bash
# Build locally
dotnet build -c Release

# Test locally
dotnet test -c Release
```

### To publish a release:
```bash
# Create a version tag and push
git tag v0.2.0
git push origin v0.2.0

# GitHub Actions will automatically:
# 1. Build and test
# 2. Create publishable binaries
# 3. Generate release on GitHub with downloads
```

### For users:
1. Go to [GitHub Releases](https://github.com/ppotepa/BarcodeRevealTool/releases)
2. Download the appropriate ZIP (win-x64 or win-arm64)
3. Extract and run `BarcodeRevealTool.exe`

---

## Troubleshooting

### Release workflow failed
Check the **Actions** tab on GitHub to see detailed logs.

Common issues:
- **Wrong project path**: Verify `src/console-app/` path in release.yml
- **Missing appsettings.json**: Ensure it's in the publish directory
- **Signing issues**: Check that no code signing is configured that might interfere

### Build takes too long
- Builds run in parallel for Debug/Release configs (use `matrix` strategy)
- First build on new runner will restore all NuGet packages (~2-3 min)
- Subsequent builds are cached

### Tests not running
- Currently tests only run on Release configuration
- Tests continue on error (they won't fail the build)
- View detailed results in the uploaded TRX files

---

## Environment Details

- **OS**: Windows Latest (GitHub-hosted runners)
- **.NET Version**: 8.0.x (LTS)
- **Runtime Identifiers**: 
  - `win-x64`: For x86-64 processors
  - `win-arm64`: For ARM64 processors

