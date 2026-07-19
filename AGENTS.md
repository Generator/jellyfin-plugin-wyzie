# AGENTS.md

## Build & Release

**Build solution (all targets):**
```bash
dotnet build -c Release Wyzie.sln
```

**Build specific Jellyfin target:**
```bash
dotnet build -p:JellyfinVersion=10.9   # default
dotnet build -p:JellyfinVersion=10.10
dotnet build -p:JellyfinVersion=10.11  # targets net9.0
```

**Package for release:**
```bash
python3 build/package.py --version 1.0.0 --repo owner/repo
```

**Release workflow:** Tag-push (`git tag v1.0.0.0 && git push origin v1.0.0.0`) triggers `.github/workflows/release.yml` which builds, packages, attaches zips to GitHub Release, and commits updated `manifest.json`.

## Architecture

Three projects in `Wyzie.sln`:

| Project | TFM | Purpose |
|---|---|---|
| `src/Wyzie.Common` | netstandard2.0 | Shared API client, models, token encoding |
| `src/Jellyfin.Plugin.Wyzie` | net8.0 / net9.0 | Jellyfin 10.9+ subtitle provider |
| `src/Emby.Plugin.Wyzie` | netstandard2.0 | Emby 4.8+ subtitle provider |

**Key relationships:**
- Jellyfin plugin references `Wyzie.Common` via `ProjectReference`
- Emby plugin **includes** `Wyzie.Common` source via `<Compile Include>` (not a project reference)
- Both plugins have separate `PluginConfiguration.cs` classes (different base classes per server)

**Multi-target Jellyfin builds:** `Jellyfin.csproj` uses conditional `<ItemGroup>` blocks to pull the correct `Jellyfin.Controller` NuGet version per target. 10.9/10.10 use net8.0; 10.11 uses net9.0.

## Subtitle Streaming Design

Plugin packs subtitle URL + format + language into a base64url token (`WyzieToken.Encode`). The token is returned as `RemoteSubtitleInfo.Id`. On `GetSubtitles(id)`, the token is decoded and the subtitle is streamed directly from Wyzie API via `HttpCompletionOption.ResponseHeadersRead` — no disk caching.

## Configuration

- API key required (free from https://sub.wyzie.io/redeem)
- Config pages are embedded resources (`Configuration/configPage.html`)
- Jellyfin config: single HTML page
- Emby config: separate HTML + JS files (Emby requires `IHasThumbImage`)

## Documentation References

- **Wyzie Subs API**: https://docs.wyzie.io/subs/usage/direct — Direct usage docs for the subtitle search/streaming API this plugin consumes
- **Wyzie Subs upstream**: https://github.com/wyziedevs/wyzie-subs — Upstream Wyzie source (useful for API behavior reference)
- **Jellyfin server**: https://deepwiki.com/jellyfin/jellyfin — DeepWiki for Jellyfin server internals, plugin APIs, and subtitle provider contracts

## Workflow: Finding Jellyfin Plugin Examples

When implementing new features or debugging issues, use `grep_searchGitHub` to find real-world patterns from other Jellyfin plugins:

```bash
# Find subtitle provider implementations
grep_searchGitHub(query="ISubtitleProvider", language=["C#"], repo="jellyfin/jellyfin")

# Find plugin configuration patterns
grep_searchGitHub(query="BasePluginConfiguration", language=["C#"], repo="jellyfin/")

# Find how other plugins handle HTTP clients and retries
grep_searchGitHub(query="IHttpClientFactory", language=["C#"], repo="jellyfin/")

# Find embedded resource config page patterns
grep_searchGitHub(query="EmbeddedResource", language=["C#"], path="Configuration/")
```

Focus on `jellyfin/jellyfin` core and `jellyfin-meta-plugins` for canonical examples.

## Workflow: GitHub CLI (`gh`)

Use the `gh` CLI for repository and workflow management:

```bash
# List recent workflow runs
gh run list --repo Pukabyte/jellyfin-plugin-wyzie

# Watch a running workflow
gh run watch <run-id>

# Trigger a workflow manually (e.g., release with a specific version)
gh workflow run release.yml --repo Pukabyte/jellyfin-plugin-wyzie -f version=1.0.3

# Create a release tag and push (triggers release workflow)
git tag v1.0.3.0 && git push origin v1.0.3.0

# Check release assets and downloads
gh release list
gh release view v1.0.3

# Download a specific release asset
gh release download v1.0.3

# View issues and PRs
gh issue list
gh pr list
gh pr view <pr-number>
```

## Conventions

- Nullable enabled, warnings as errors (`TreatWarningsAsErrors` in Common)
- `LangVersion=latest` across all projects
- No test suite present
- No lint/format tooling configured
- `manifest.json` is auto-updated by release workflow — don't edit manually unless adding a new plugin
- Plugin GUID: `b2c9f7a0-2d4e-4b8f-9a1c-7e3d4c5a6b70` (Jellyfin), `c3d9f7a0-2d4e-4b8f-9a1c-7e3d4c5a6b71` (Emby)
