# 🎤 KHost Cue

> **You're on cue.** — the singer & patron companion app for [KHost](../KHost), open-source karaoke host software.

[![Platform](https://img.shields.io/badge/platform-iOS%20%7C%20Android-blueviolet)](#)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![UI](https://img.shields.io/badge/UI-MAUI%20Blazor%20Hybrid-5C2D91)](https://learn.microsoft.com/dotnet/maui/)
[![License](https://img.shields.io/badge/license-PolyForm%20Shield%201.0.0-orange)](LICENSE)

KHost Cue is a cross-platform mobile app for **iOS and Android** that keeps a personal karaoke wishlist in your pocket. Add songs you'd love to sing, line up a set for the night, rate how each performance went, and look up lyrics on the spot — with your list stored **on your device**.

## 📸 Screenshots

<p align="center">
  <img src="docs/screenshots/my-songs.png" width="200" alt="My List — your karaoke wishlist as sortable, swipeable cards, with the Tonight tab in the bottom bar" />
  <img src="docs/screenshots/tonight.png" width="200" alt="Tonight — the on-deck set list on its own tab: drag to reorder and check each song off as you sing it" />
  <img src="docs/screenshots/song-detail.png" width="200" alt="Song detail — per-performance ratings, enjoyment, sung history, and quick links" />
  <br />
  <img src="docs/screenshots/lyrics.png" width="200" alt="Lyrics — looked up in-app from LRCLIB" />
  <img src="docs/screenshots/settings.png" width="200" alt="Settings — feature toggles grouped by what they affect, in collapsible sections" />
  <img src="docs/screenshots/import-export.png" width="200" alt="Import & Export — pull songs from a Spotify or YouTube Music playlist or a KHost Cue file, and export your list back out" />
</p>

## ✨ Features

- **Tonight set list** — build an on-deck set for the venue on its own tab: add songs, drag to reorder, and check each one off as you sing it, with live *“X of Y done”* progress. It's kept separate from your wishlist, so a song you sang earlier today stays unchecked until you check it off here.
- **My Songs** — a personal wishlist of songs to sing, as a swipeable card list with sorting and multi-select filters (text, genre, year, how-it-went / enjoyment rating). One tap on a card adds a song straight to tonight's set.
- **Ratings & history** — rate *every* performance (a "how it went" score that averages over time) plus a separate **enjoyment** rating, jot per-performance notes, and keep a running sung-history for each song.
- **Lyrics** — look a song's lyrics up in-app from **[LRCLIB](https://lrclib.net/)** (no account needed) and cache them on-device so they open instantly next time.
- **Surprise me** — can't decide what to sing? One tap picks a random song for you — and it can skip anything you've already sung today.
- **Quick links** — jump straight to any song on **YouTube** or **Spotify**.
- **Favorites** — star the songs you love; they float to the top of the list.
- **Import & export** — pull songs from a public **Spotify** or **YouTube Music** playlist link, or a KHost Cue `.json` file, and export your whole list back out.
- **Auto-fill** — looks up a song's release year and genre automatically (via the iTunes Search API) so you don't have to type them.
- **Update alerts** — tells you when a newer version is available (from the app's GitHub Releases) with a one-tap link to grab it.
- **Made to feel at home** — mobile-first layout, light & dark themes, and a tidy Settings screen where every extra behavior can be toggled off.

## 🛠️ Tech stack

- **[.NET 10](https://dotnet.microsoft.com/)** with **[.NET MAUI Blazor Hybrid](https://learn.microsoft.com/dotnet/maui/)** — native iOS/Android shell hosting a Razor (Blazor) UI.
- On-device storage in JSON files behind interfaces (`ISongListStore`, `ITonightStore`, `ILyricsCache`) that keep storage concerns out of the UI.

## 🚀 Getting started

### Prerequisites

- **.NET 10 SDK** with the MAUI workload:
  ```bash
  dotnet workload install maui
  ```
- **Android**: the Android SDK and an emulator or a connected device.
- **iOS**: a paired Mac (iOS cannot be built on Windows).

### Build & run

```bash
# Android
dotnet build KHost.Mobile/KHost.Mobile.csproj -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Deploy and launch on a connected Android device / emulator
dotnet build KHost.Mobile/KHost.Mobile.csproj -t:Run -f net10.0-android "-p:BaseOutputPath=./obj/_build"

# Run on Windows — the quickest way to iterate on the Blazor UI (no emulator needed)
dotnet run --project KHost.Mobile -f net10.0-windows10.0.19041.0 "-p:BaseOutputPath=./obj/_build"
```

> `-p:BaseOutputPath=./obj/_build` keeps build output out of the IDE's `bin/` folder so it doesn't get locked while the IDE is open.

### Sample data for testing

Need songs to populate the list while testing? This public **YouTube Music** playlist imports cleanly via **Import & Export → YouTube Music**:

```
https://music.youtube.com/playlist?list=PLrB1lrYJ3YfvS2ZaTJZ_D8vvIv_fowkNM
```

## 📁 Project structure

| Project | Role |
|---|---|
| `KHost.Mobile` | The MAUI Blazor Hybrid app — a thin native shell hosting the Razor UI in `Components/`. |
| `KHost.Mobile.Client` | Client library: playlist import (Spotify / YouTube Music), iTunes metadata lookup, and LRCLIB lyrics lookup. |

## 🤝 Contributing

Issues and pull requests are welcome. Please keep changes focused and describe the behavior you're changing.

## 📄 License

KHost Cue is licensed under the [PolyForm Shield License 1.0.0](LICENSE) — the same license as [KHost](../KHost).

You may use, modify, and self-host it for any purpose, **including commercial use** (for example, running it for your own karaoke events). You may **not** use it to provide a competing product or service — such as a hosted/managed offering (SaaS) or a rebranded redistribution — without a separate license. Commercial, SaaS, and OEM licenses are available; contact Michael Riddle <riddlemd@gmail.com>.

Third-party components bundled in the app are listed, with their licenses, in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) (all MIT / Apache-2.0).
