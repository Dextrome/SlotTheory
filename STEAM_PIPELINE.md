# Steam Release Pipeline

Checklist from zero to a downloadable Steam build. Work top to bottom.
Each section has a clear done-state so you know when to move on.

---

## 1. Godot export templates (local — do this first)

**Done when:** `./build_release.sh` completes without errors and `export/SlotTheory.exe` launches.

- [ ] Open Godot editor
- [ ] Editor → Manage Export Templates → Download and Install
  - Must match exact version: `4.6.1 stable mono`
  - ~500 MB download
- [ ] Run `./build_release.sh` from the project root
- [ ] Verify `export/SlotTheory.exe` launches and plays correctly

---

## 2. Steamworks account and app registration

**Done when:** You have an App ID and can see the app in Steamworks.

- [ ] Sign in at https://partner.steamgames.com
- [ ] Pay the $100 app fee if not already done (refunded after $1000 gross revenue)
- [ ] Create new App → type: Game
- [ ] Note your **App ID** (you will use it everywhere below)
- [ ] Set app name: `Slot Theory`
- [ ] Under **General** → confirm app name, developer name (`7ants Studios`), publisher name

---

## 3. Depot setup

**Done when:** You have one Windows depot ID and SteamPipe is configured.

- [ ] In Steamworks → SteamPipe → Depots
- [ ] Create one depot: `Slot Theory - Windows` (OS: Windows, Architecture: x86_64)
- [ ] Note the **Depot ID** (App ID + 1 by default, e.g. if App ID = 123456, Depot = 123457)

---

## 4. SteamCMD and build scripts

**Done when:** `steamcmd` can log in and `run_app_build` executes without errors.

- [ ] Download and install SteamCMD: https://developer.valvesoftware.com/wiki/SteamCMD
  - Unzip to `C:\steamcmd\`
- [ ] Create `steam/app_build.vdf` in this repo (see template below)
- [ ] Create `steam/depot_build.vdf` (see template below)
- [ ] Test login: `steamcmd +login <username> +quit`
  - First login prompts Steam Guard code — run it once interactively

**`steam/app_build.vdf`** — fill in your App ID:
```
"AppBuild"
{
    "AppID"     "YOUR_APP_ID"
    "Desc"      "Slot Theory build"
    "BuildOutput" "../steam/output/"
    "Depots"
    {
        "YOUR_DEPOT_ID"
        {
            "FileMapping"
            {
                "LocalPath" "../export/*"
                "DepotPath" "."
                "recursive"  "1"
            }
        }
    }
}
```

**`steam/depot_build.vdf`** — not required if depot is embedded in app_build.vdf above.

**`steam/push_build.sh`** — upload script:
```bash
#!/usr/bin/env bash
set -e
./build_release.sh
/c/steamcmd/steamcmd.exe \
  +login YOUR_STEAM_USERNAME \
  +run_app_build "$(pwd)/steam/app_build.vdf" \
  +quit
```

- [ ] Add `steam/output/` to `.gitignore`
- [ ] Run `./steam/push_build.sh` and confirm build appears in Steamworks → SteamPipe → Builds

---

## 5. Set default branch

**Done when:** You can install and run the game via the Steam client using your own account.

- [ ] In Steamworks → SteamPipe → Builds
- [ ] Find your uploaded build → set it as default on branch `default` (or create a `beta` branch)
- [ ] Grant your own Steam account a complimentary key or developer license
- [ ] Install the game via Steam client → verify it launches

---

## 6. Store page — minimum required for review

**Done when:** Steam review queue accepts the submission (they check these assets exist).

Steam requires all of these before submission:

| Asset | Size | Notes |
|-------|------|-------|
| Capsule image (small) | 231×87 px | shown in search results |
| Capsule image (large) | 460×215 px | shown on store page header |
| Header capsule | 460×215 px | same as large capsule is OK |
| Library capsule | 600×900 px | shown in Steam library |
| Screenshots | min 5, 1280×720 or larger | at least 1 without UI overlay |
| Short description | 1–2 sentences | shown in search |
| Long description | 1+ paragraphs | full store page body |
| Tags | 3–5 minimum | Tower Defense, Strategy, Roguelike, etc. |
| System requirements | Windows 10+, 4 GB RAM, no GPU requirement is fine | |
| Age rating | fill out IARC questionnaire (free, ~5 min) | |
| Privacy policy URL | required even if minimal | can be a simple hosted page |

- [ ] Create placeholder capsule assets (even rough ones get you past review)
- [ ] Write short + long description
- [ ] Fill out IARC age rating questionnaire
- [ ] Fill out system requirements
- [ ] Add a privacy policy URL
- [ ] Set price (or Free to Play)
- [ ] Submit for review

---

## 7. Valve review

**Done when:** Email from Valve confirms the app passed review (~3–5 business days).

- [ ] Wait for review result
- [ ] If rejected: read the rejection reason, fix it, resubmit
- [ ] After approval: store page is live (visible to you, coming soon state)

---

## 8. Release

**Done when:** The app is publicly visible and purchasable on Steam.

- [ ] Set a release date (or "Coming Soon" while finishing polish)
- [ ] Upload final build via `./steam/push_build.sh`
- [ ] Set that build as default on `default` branch
- [ ] Click **Release App** in Steamworks
- [ ] Announce on any channels you have

---

## Notes

- **Start steps 2–4 now**, in parallel with balance/polish work. Valve review alone takes days.
- `export_presets.cfg` is already configured. `build_release.sh` is ready. Only export templates are missing.
- The `steam/` directory and `steam/output/` should be in `.gitignore`.
- Code signing (step 4 codesign) is optional for initial release but reduces Windows Defender false positives.
