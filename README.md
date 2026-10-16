# Jellyfin Custom Logo Plugin

Replaces the Jellyfin logos in the web client with your own images — uploaded directly on the plugin's settings page.

| Slot | Where it appears | Recommended size |
|---|---|---|
| **Banner** | Page header, loading screen (large displays) | about 1000 × 250 px, transparent PNG |
| **Icon** | Side menu, server button, TV layout, loading screen (small displays), logo screensaver | 512 × 512 px |
| **Favicon** | Browser tab, home-screen / app icon | 512 × 512 px PNG or ICO |

Accepted formats: PNG, JPEG, GIF, WebP, ICO (max. 10 MB). SVG is deliberately blocked because it can contain scripts.
Options: turn replacement on/off without deleting uploads; use the icon as a fallback for the banner and/or favicon.

## Compatibility

| Jellyfin | Plugin build | .NET |
|---|---|---|
| **12.x** (12.0, 12.1, 12.2 …) | `custom-logo_<version>.12.zip` | 10 |
| **10.11.x** | `custom-logo_<version>.10.zip` | 9 |

When installing from the repository, Jellyfin automatically picks the right build.

## Installation

### Via plugin repository (recommended)

1. Dashboard → **Plugins** → **Repositories** → **+**
2. Name: `Custom Logo`, URL:
   ```
   https://raw.githubusercontent.com/tinyit90/CustomLogoPlugin-Jellyfin/main/manifest.json   ```
   
3. **Catalog** → **Custom Logo** → **Install**, then restart Jellyfin.

### Manual installation

1. Download the zip that matches your Jellyfin version from the [releases](../../releases).
2. Extract it into a new folder within the `plugins` folder of your Jellyfin data directory, e.g.
   - Linux: `/var/lib/jellyfin/plugins/CustomLogo_2.0.0.12/`
   - Docker: `/config/plugins/CustomLogo_2.0.0.12/`
   - Windows: `%ProgramData%\Jellyfin\Server\plugins\CustomLogo_2.0.0.12\`
3. Restart Jellyfin.

> If an old version of the plugin (1.0.0) is installed, uninstall it first — or simply delete its folder in `plugins`.

## Usage

Dashboard → **Plugins** → **Custom Logo**. Click **Upload** for each slot and choose an image. The change takes effect immediately; reload the web client afterward.

If the old logo is still displayed: clear the browser cache once (Ctrl + F5 / Cmd + Shift + R). Native apps (Android TV, iOS, etc.) display their own built-in logos in some places — the plugin only affects the web client and apps that load the web client (e.g. Jellyfin Media Player, desktop app).

The images are stored in `<config>/plugins/configurations/CustomLogo/` and survive plugin updates.

## How it works

jellyfin-web bundles its logos as files like `banner-light.<hash>.png`, `icon-transparent.<hash>.png`, `favicon.<hash>.ico` and `favicons/touchicon*.png`. The plugin registers a small middleware in the Jellyfin server that responds to requests for exactly these files with the uploaded image (by file name, regardless of the hash — so it keeps working after web client updates). No files in the Jellyfin installation are modified; disabling or uninstalling the plugin immediately restores the original logos.

API (administrators only, except preview):

| Method | Path | |
|---|---|---|
| `GET` | `/CustomLogo/Logos` | Status of all slots |
| `GET` | `/CustomLogo/Logos/{Banner\|Icon\|Favicon}` | Uploaded image (preview) |
| `POST` | `/CustomLogo/Logos/{slot}` | Upload — raw image file as request body |
| `DELETE` | `/CustomLogo/Logos/{slot}` | Remove |

## Building

Requires the [.NET SDK 10](https://dotnet.microsoft.com/download) (for the 10.11 build, additionally .NET SDK 9 or just 10) and Python 3.

```bash
./build.sh          # Jellyfin 12.x
./build.sh 10.11    # Jellyfin 10.11.x
./build.sh all      # both
```

The installable zips end up in `dist/`.

## Publishing a release

1. Push the repository to your own GitHub account (fork or new repo named `CustomLogoPlugin`).
2. Under **Settings → Actions → General → Workflow permissions**, select "Read and write permissions".
3. Create and push a tag:
   ```bash
   git tag -a v2.0.0 -m "Rebuilt for Jellyfin 12"
   git push origin v2.0.0
   ```
The **Release** workflow builds both versions, creates the GitHub release with the zips and updates `manifest.json` — after that, the repository URL above works.

## License

MIT — see [LICENSE](LICENSE). Originally by [bvolvy](https://github.com/bvolvy/CustomLogoPlugin).
