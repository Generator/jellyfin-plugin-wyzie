# Wyzie Subtitles — Jellyfin plugin

On-demand subtitle provider backed by the [Wyzie Subs](https://sub.wyzie.io) API.
The plugin streams subtitle content directly from Wyzie when the media server
requests it — it does not cache files on disk.

> **API key required.** Claim a free key at
> [sub.wyzie.io/redeem](https://sub.wyzie.io/redeem) and paste it into the plugin
> configuration page before use.

## Projects

| Project | Target | Purpose |
|---|---|---|
| `src/Wyzie.Common` | netstandard2.0 | API client, models, shared helpers |
| `src/Jellyfin.Plugin.Wyzie` | net10.0 | Jellyfin 12.0 provider |

## Build

```bash
dotnet build -c Release -p:JellyfinVersion=12.0 src/Jellyfin.Plugin.Wyzie/Jellyfin.Plugin.Wyzie.csproj
```

The plugin targets Jellyfin 12.0 (`net10.0`) and builds standalone on the
.NET 10 SDK.

## Install

### Jellyfin (plugin repository — recommended)

`Dashboard → Plugins → Repositories → +` and add:

```
https://raw.githubusercontent.com/Generator/jellyfin-plugin-wyzie/gh-pages/manifest.json
```

Then `Catalog → Subtitles → Wyzie Subtitles → Install`, restart the server,
paste your API key under `Plugins → Wyzie Subtitles`.

### Jellyfin (manual)

Download `Jellyfin.Plugin.Wyzie.zip` from the
[releases page](../../releases), extract into
`<data>/plugins/Wyzie Subtitles_<version>/`, restart.

### Release workflow

Tag-push triggers the `.github/workflows/release.yml` pipeline: builds the
plugin for Jellyfin 12.0, generates `meta.json` + zip, attaches them to a
GitHub Release, and the `jellyfin-plugin-repo-action` regenerates
`manifest.json` on the `gh-pages` branch (the catalog URL above) from the
release.

```bash
git tag v1.0.5 -m "Release 1.0.5"
git push origin v1.0.5
```

## Configuration

- **API key** — required; see https://sub.wyzie.io/redeem.
- **Hearing impaired** — include/exclude SDH subs.
- **Preferred source** — pin to a specific Wyzie source, or "all".
- **Preferred format** — `srt` (default), `ass`, or `sub`.
- **Max retries** — backoff attempts on 429/5xx from the API.

## On-demand streaming

The plugin packs the subtitle's direct URL into an opaque token returned as the
`RemoteSubtitleInfo.Id`. When Jellyfin calls `GetSubtitles(id)`, the token
is decoded and the subtitle is streamed straight from Wyzie via
`HttpCompletionOption.ResponseHeadersRead` — the plugin never writes the file
to disk. (The server may still persist the picked subtitle next to the media
per its own `DownloadLanguages` / `SaveSubtitlesWithMedia` settings; disable
those if you want zero disk writes.)

## API

Uses `https://sub.wyzie.io/search` with `?key=<API_KEY>`. See
[`src/Wyzie.Common/WyzieClient.cs`](src/Wyzie.Common/WyzieClient.cs).
