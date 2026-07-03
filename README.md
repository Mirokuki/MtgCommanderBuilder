# 🎴 MTG Commander Builder

A premium, feature-rich desktop application built with **WPF** and **.NET 9** designed to facilitate Magic: The Gathering Commander deck building, visual curation, power-level analysis, and high-quality proxy sheet printing. 

Features modern Windows 11 aesthetics with fully custom styling via **WPF-UI** in an obsidian dark mode environment.

---

## 🌟 Features

### 1. 🗃️ Offline Scryfall Database & Search
- Full offline search capabilities utilizing a local Scryfall database sync.
- Supports advanced search filters matching native Scryfall query syntax (e.g., color identity, mana value, type, set, and rarity).
- Offline-ready database manager directly accessible in the settings/menu.

### 2. 🎽 Deck Builder & Workspace
- **Moxfield-Style Card Spoiler Grid**: View full card images, live price updates, and commander color requirements in a visually rich grid.
- **Spreadsheet List View**: A compact list for quick review, filtering, and sorting.
- **Interactive Card Zoom**: Dynamic slider scaling card sizes from `50%` to `200%` with automatic layout reflow.
- **Smooth Marquee Hover Auto-Scroll**: Hovering over long deck names in the navigation panel scrolls the text smoothly so no characters are clipped.
- **Save/Delete Operations**: Context menu options with delete confirmations to prevent accidental loss of deck files.

### 3. 🧙 Deck Wizard (AI-Powered Recommendations)
- Assists in identifying and achieving deck balance goals.
- Provides recommendations based on power level (Casual, Optimized, Competitive).
- Recommends card categories like Mana Ramp, Draw, Single Target Removal, Board Wipes, and Protection.

### 4. 🧪 Stats & Mana Designer
- **Mana Distribution**: Automated curve analysis and land recommendations based on commander color identities.
- **Live Metrics**: Displays overall deck size, estimated total cost, and commander tax tracking.

### 5. 🖨️ Cricut-Ready Proxy Printer
- Renders printable PDF pages of proxy cards in Cricut-compatible layouts.
- Dynamic margins, dashed guide lines, bleed edges, and custom page sizes.
- Live page pre-viewing, quantity increment/decrement controls, and print sheet management.
- Automatically cleans up local EDHrec cache recommendations for deleted commanders.

---

## 🛠️ Tech Stack

- **Framework**: .NET 9.0 (Windows Desktop WPF)
- **UI Components**: WPF-UI (Modern styling engine)
- **Data Access & Cache**: Local JSON-based storage for decks, app settings, and cached EDHrec recommendation files.
- **API integrations**: Scryfall JSON API for database synchronization; EDHrec JSON api.

---

## 🚀 Getting Started

### Prerequisites
- [Windows 10 / 11](https://www.microsoft.com/windows)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Installation & Run
1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/MtgCommanderBuilder.git
   cd MtgCommanderBuilder
   ```
2. Build the project:
   ```bash
   dotnet build -c Release
   ```
3. Run the application:
   ```bash
   dotnet run --project MtgCommanderBuilder.csproj
   ```

### First-Time Setup
On the first launch:
1. Click the `...` menu next to the **Save Deck** button.
2. Select **Sync from Scryfall (Offline Setup)** to download the latest offline card database.
3. Once completed, search and build your decks immediately offline!

---

## 🧹 Cache Maintenance
To optimize storage, the application automatically tracks commander usage across all your saved decks. When you delete a deck, the program checks if its commander is in use elsewhere. If not, it automatically deletes the cached recommendations JSON file (under `AppData/Roaming/MtgCommanderBuilder/Data/EdhrecCache/`), keeping your system clean.
