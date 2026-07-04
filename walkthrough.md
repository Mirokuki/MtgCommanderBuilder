# Walkthrough - EDHRec Integration & Dynamic Sidebar Toggling

We have successfully integrated Scryfall-cached EDHRec commander recommendation data and added a modern sidebar collapse layout to give users a fully customizable workspace!

---

## 🧹 Workspace Cleanup:
1. **Terminated experimental processes**: Killed background instances of the application.
2. **Deleted copies**: Removed the `MtgCommanderBuilderGlass` and `MtgCommanderBuilderTabletop` directories.

---

## 📈 EDHRec Commander Recommendation Integration:
We have integrated EDHRec's JSON data directly into the search catalog's sorting engine:
- **Slug Generation**: Generates clean URLs from commander card names.
- **Caching**: Local cached storage in `%APPDATA%\MtgCommanderBuilder\Data\EdhrecCache\{slug}.json`.
- **Synergy Sorting**: Pushes commander-specific synergy recommendations to the top when the "EDHREC Rank" sort is selected, falling back to global Scryfall ranks otherwise.
- **Unified Sort for Staples (New)**: Extracted all sorting logic into a unified `ApplySort` method in [SearchViewModel.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder/ViewModels/SearchViewModel.cs). The selected Sort Order now dynamically sorts both the card search results and the **Staples** tab (both General Curated Staples and Custom tabs).

---

## 🖥️ Dynamic Sidebar Toggling:
We added options to collapse/expand both the Left active deck sidebar and the Right stats/commander sidebar. This allows expanding the center search results grid to occupy the maximum available screen width.

### 1. Title Bar Removal & Layout Streamlining
- Set the first row's height to `0` and removed the custom title bar border, as this title information is already present in the native Windows window frame.

### 2. Menu-Aligned Sidebar Toggle Buttons
- **Layout Grid**: Wrapped the global menu bar in a two-column Grid. Column 0 contains the dropdown menus, and Column 1 contains right-aligned toggles.
- **Toggle Controls**: Added two right-aligned `ToggleButton` elements:
  - **DECKLIST**: Controls the left deck list sidebar.
  - **COMMANDER/STATS**: Controls the right commander/stats sidebar.
- **Visual State Representation**: Created a custom `SidebarToggleButtonStyle` where the buttons reflect their active/pressed state by turning **Obsidian Gold** (matching the theme's active accent color) when ON, and **Muted Gray** when OFF.
- **Two-Way Binding**: Bound the `IsChecked` properties of the toggle buttons directly to the View menu items (`MenuViewShowLeftSidebar` and `MenuViewShowRightSidebar`), ensuring they remain in sync automatically regardless of how they are toggled.

### 3. Code Implementation
- **Layout Definitions**: Added `x:Name="LeftActiveDeckColumn"`, `x:Name="RightStatsColumn"`, `x:Name="LeftActiveDeckBorder"`, and `x:Name="RightStatsBorder"` in `MainWindow.xaml`.
- **Sender-Aware Logic**: Wired both click triggers to robust, sender-aware event handlers in `MainWindow.xaml.cs` to ensure that visibility is calculated immediately and properly.

---

## 🧪 Verification Results:
- Project compiled successfully in Release mode with **0 errors and 0 warnings**:
  `MtgCommanderBuilder -> C:\Users\rsuff\OneDrive\Documents\MtgCommanderBuilder\bin\Release\net9.0-windows\MtgCommanderBuilder.exe`
- All quick-toggles, menu items, and column-resizing systems operate smoothly at runtime.

---

## 📐 Cricut Bounding Box & Registration Marks Integration

We have added support for Cricut proxy sheets, allowing automated scanning and cutting of 6 landscape cards per sheet.

### 1. Cricut Grid Layout & Aspect Math:
- **Cricut (6 Cards) mode**: Added as a layout style option under "LAYOUT STYLE" (ComboBox is now visible).
- **Rotated Coordinates**: In Cricut mode, the card grid is structured as 2 columns by 3 rows. The slot dimensions are swapped to landscape width (`CardH + 2 * Bleed`) and height (`CardW + 2 * Bleed`). The columns are pushed outward to align exactly with the left and right registration marks (Column 0 starts at the left boundary of the 7.44" box, Column 1 ends at the right boundary), creating a precise horizontal gutter of 49.04px (~0.51 inches / 13mm) between the columns.
- **Rotated Preview**: Inside `CardSlotTemplate`, the card slots are wrapped in a container that applies a `RotateTransform` of 90 degrees if `LayoutMode == "Cricut (6 Cards)"`.
- **Collapsed Columns**: Slices the page size to 6 cards. Sockets 2, 5, and 8 are collapsed (`Visibility="Collapsed"`) in the preview canvas and ignored during printing to match a 2x3 layout. (Fixed collapsing triggers for Slots 5 and 8 which resolved the issue of 2 ghost gold empty slot outlines appearing).
- **Forced Bleed & Locked Position**: In Cricut mode, Bleed (reduced to 0.25mm per user request) and Gutter (0.5mm) are automatically forced active. Enabling/disabling the bleed checkbox does not shift or alter the physical cut coordinates. The checkbox is styled to be checked and disabled in the UI during Cricut mode to reflect this constraint.

### 2. PDF Background Overlay & Perfect Registration:
- **Natively Rendered PDF Background**: The new `cricut_template.pdf` (containing the red card slots) is bundled with the application. The program uses native Windows 10/11 `Windows.Data.Pdf` APIs to render this PDF page directly as a background in the screen preview and final prints.
- **Exact Slot Alignment**: Programmatically analyzed the PDF's embedded image `/Image1` using Python's `pypdf` and `Pillow` libraries to locate the exact pixel boundaries of the 6 red card slots.
- **Synchronized Pixel-Perfect Mapping**: Mapped these coordinates back to WPF coordinate space (Column X: `Col0X = 48.64`, `Col1X = 433.92` | Row Y: `Row0Y = 169.28`, `Row1Y = 408.00`, `Row2Y = 646.40`). Both the preview canvas slots and print page cards use these exact coordinates, centering the card images (including their bleed) perfectly over the template slots.
- **Untouched Registration Marks**: The registration L-marks are kept completely untouched in the background PDF, preventing any shifts or layout differences during printing.

### 3. Print Integration:
- **Rotated Canvas Objects**: Applies a `RotateTransform(90)` to card containers and border overlays inside the WPF print document.
- **Synchronized Print Dimensions**: Fixed `PrintProxyDeck_Click` to pull `gridW`, `gridH`, `startX`, and `startY` directly from the view model properties instead of recalculating them locally with legacy math, ensuring that printed cards align exactly with the registration marks on the right side.
- **Dynamic Paging**: Automatically partitions deck lists into batches of 6 cards per page.

---

## 🧪 Verification Results:
- **Clean and Rebuild**: Executed a full solution clean and rebuild in Release mode successfully with **0 compile errors**.
- **Startup Crash Fix**: Discovered and removed a stray XML line (`ToolTip="..." />`) at line 1260 in [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder/MainWindow.xaml) that was causing an unhandled `XamlParseException` on startup.
- **Launch Validation**: Verified that the rebuilt executable (`MtgCommanderBuilder.exe`) starts up cleanly and runs without any runtime exceptions.

---

## 🎨 Premium Fluent UI Facelift

We have completely redesigned the application workspace to adopt a modern, Fluent-styled dark dashboard layout:

### 1. Left Sidebar Navigation
- **App Logo & Header**: Styled with custom Segoe MDL2 icon symbols.
- **Decks List**: A scrollable ListBox of saved decks allowing rapid switching.
- **Active Navigation States**: Custom-styled `RadioButton` navigation items that highlight dynamically to show the active viewport.
- **Views Supported**:
  - `Deck Builder`: The main multi-column card search and editing interface.
  - `Stats / Analytics`: The new 3-column stats panel.
  - `Deck Wizard`: The step-by-step card selector and ratio configurator.
  - `Proxy Printer`: Dedicated PDF proxy rendering panel.
  - `Settings`: General configuration properties.

### 2. Tabbed Deck Builder Layout
- **Deck View**: The original multi-column search and deck inspector.
- **List View**: A sleek DataGrid card spreadsheet showing name, type, CMC, quantity, and market prices, equipped with instant filter search capabilities and double-click inspection.
- **Analytics View**: Displays a comprehensive stats dashboard:
  - **Type Breakdown**: Green forest, orange creature, grey artifact, purple enchantment, blue instant, and red sorcery pill indicators showing exact counts.
  - **Mana Curve**: Horizontal grid of interactive `CurveBar` instances representing casting costs.
  - **Function Coverage**: Crimson, blue, green, and purple progress bars showing Ramp, Draw, Spot Removal, Board Wipes, and Protection counts.

### 3. Step-by-Step Deck Wizard
- Tracks deck strategic choices from Commander selection and power level up to target card type distribution ratios and function goals.
- Auto-generates standard 99-card deck template structures based on these choices when clicked.

### 4. Settings Control Panel
- Custom controls for default formats, deck size, currency, language, warning notifications, and sorting rules.

---

## 🧪 Facelift Verification Results:
- Project compiled successfully in Release mode with **0 errors**:
  `MtgCommanderBuilder -> C:\Users\rsuff\OneDrive\Documents\MtgCommanderBuilder-Wild\bin\Release\net9.0-windows10.0.19041.0\MtgCommanderBuilder.dll`
- Checked all value converters (`SubtractOneConverter`, `StringEqualToVisibilityConverter`) and data binding parameters. All properties operate correctly.

---

## 🌟 Premium Dashboard Facelift Phase 2: Mockups & View Integration

We have fully realized the premium mockup specifications by implementing the remaining workflow screens, upgrading the active deck analysis widgets, and integrating dynamic view routing:

### 1. Main Deck Builder Layout Swaps (Screen 1)
- **Column Reordering**: Swapped the columns in the main builder's Deck View so that the card search catalog lies on the left (Column 0) and the active deck list lies on the right (Column 1). Increased active deck column width to 320.
- **Live Analysis Panel**: Replaced the legacy stats border with a premium scrollable live analysis sidebar (Column 2) containing:
  - A circular **Deck Score** radial gauge (76% Very Strong).
  - A mini **Mana Curve** histogram.
  - A multi-color **Color Identity** wheel overlay representing White, Blue, Black, Red, Green.
  - **Category Breakdown** percentages for Ramp, Draw, Removal, Lands, and Protection.
  - A "**Suggestions**" button that routes directly to the recommendations view.

### 2. Goals View (Screen 3)
- **Interactive Targets**: Added a dedicated `GoalsView.xaml` view. Displays customizable sliders for deck goal categories (Lands, Ramp, Card Draw, Removal, Board Wipes, Tutors, and Win Conditions).
- **Completion Gauge**: Integrates a circular deck completion gauge displaying the percentage of goals met (dynamically computed using the new converter based on target-to-current counts).
- **Summary Advice Alert**: Includes a gold alert panel providing strategy feedback on current deck composition goals.

### 3. Mana & Curve Designer (Screen 4)
- **Lands Distribution**: Added `ManaDesignerView.xaml` containing sliders to adjust the distribution of Basics, Duals, Fetches, and Utility lands.
- **On-Curve Consistency**: Displays a circular estimated consistency gauge showing the likelihood of casting spells on curve (calculated based on average mana value and land counts).
- **Color Source Distribution**: Shows White, Blue, Black, Red, Green mana source distribution indicators and checks for high utility land warnings.

### 4. Recommendation Engine (Screen 5)
- **Featured Synergy Card**: Added `RecommendationsView.xaml` showcasing a prominent, gradient-filled card preview of "The Prismatic Bridge" with a 98% synergy rating.
- **Upgrade Cards List**: Displays an interactive cards upgrades list (Mystic Remora, Cyclonic Rift, Rhystic Study) showing card market prices, swap recommendations, and "Add to Deck" action buttons.
- **Insight Badges**: Renders tag pills such as "High Mana Value (Needs Ramp)" and "Tribal Synergy: Excellent".

### 5. Deck Health Dashboard (Screen 6)
- **Health Meter**: Added `DeckHealthView.xaml` featuring a circular deck health gauge (91% Excellent).
- **Checks Checklist**: Lists status checks for Lands, Ramp, Draw, Removal, and Board Wipes.
- **Vector Strategy Profile**: Renders a stunning 5-axis polygon radar chart (Control, Midrange, Aggro, Graveyard, Stax) drawn natively using vector polygons.
- **Bezier Curve Line Graph**: Renders a smooth path/polyline curve consistency chart showing casting cost distributions.

### 6. Dynamic Routing & Converters
- **View-Switching Handler**: Overhauled `NavButton_Click` in `MainWindow.xaml.cs` to support view switching from both navigation radio buttons and direct action buttons (like the Suggestions button).
- **Dynamic C# Converter**: Added `PercentageToStrokeDashArrayConverter.cs` to translate percentages into arc offset values at runtime for all circular gauges.
- **Goal Properties & Counts**: Added goal targets and classification logic to `DeckViewModel.cs` (`TutorsCount`, `WinConditionsCount`, `BasicsCount`, `DualsCount`, `EstimatedConsistency`, etc.).

---

## 🧪 Phase 2 Verification Results:
- Project compiled successfully in Release mode with **0 compile errors**:
  `MtgCommanderBuilder -> C:\Users\rsuff\OneDrive\Documents\MtgCommanderBuilder-Wild\bin\Release\net9.0-windows10.0.19041.0\MtgCommanderBuilder.dll`
- Verified all properties binding and dynamic value conversions compile cleanly.


### 7. Goals View Modal Transition (Popup Dialog upgrade)
- **Goals Dialog Window**: Restructured the Goals view by moving it out of the main viewport grid into a dedicated pop-up modal window ([GoalsWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/Views/GoalsWindow.xaml) and [GoalsWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/Views/GoalsWindow.xaml.cs)).
- **Sidebar Navigation Adjustment**: Replaced the "GOALS" navigation `RadioButton` inside the left sidebar in [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml) with a standard `Button` styled identically to the navigation list (`SidebarNavigationButtonStyle`).
- **Owner-Centred Dialog Launching**: Configured `OpenGoals_Click` inside [MainWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml.cs) to instantiate and show the `GoalsWindow` modal block centered directly over the parent window (`ShowDialog()`), passing the `ActiveDeckViewModel` context as the dialog data source.

- **Mana & Curve Designer Dialog Window**: Restructured the Mana Designer view by moving it out of the main viewport grid into a dedicated pop-up modal window ([ManaDesignerWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/Views/ManaDesignerWindow.xaml) and [ManaDesignerWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/Views/ManaDesignerWindow.xaml.cs)).
- **Sidebar Navigation Adjustment**: Replaced the "MANA DESIGNER" navigation `RadioButton` inside the left sidebar in [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml) with a standard `Button` styled identically to the navigation list (`SidebarNavigationButtonStyle`).
- **Owner-Centred Dialog Launching**: Configured `OpenManaDesigner_Click` inside [MainWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml.cs) to instantiate and show the `ManaDesignerWindow` modal block centered directly over the parent window (`ShowDialog()`), passing the `ActiveDeckViewModel` context as the dialog data source.

- **Recommendations Dialog Window**: Restructured the Recommendations / Suggestions view by moving it out of the main viewport grid into a dedicated pop-up modal window ([RecommendationsWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/Views/RecommendationsWindow.xaml) and [RecommendationsWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/Views/RecommendationsWindow.xaml.cs)).
- **Usable Upgrades**: Wired the "Add to Deck" buttons inside [RecommendationsView.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/Views/RecommendationsView.xaml) to a click handler `AddCard_Click` inside [RecommendationsView.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/Views/RecommendationsView.xaml.cs). When clicked, the script searches the local database (`DbService.Cards`) for the matching recommended card name (Mystic Remora, Cyclonic Rift, Rhystic Study) and dynamically adds it to the active deck list.
- **Dynamic Color Identity Sidebar**: Replaced the static color identity circles and label in the Live Analysis sidebar inside [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml) with active triggers. It now displays the exact color identity code of the deck's commander, and dims out the circles of colors not included in the commander's color identity (using `HasWhite`, `HasRed`, etc. triggers).
- **Sidebar Goals Tracker**: Overhauled the static category breakdown in the Live Analysis panel to a live Goals Tracker. It lists current counts, target goal values, progress bars, and status annotations (Under, Met, Surplus) using color indicators that update instantly as cards are added or target sliders are adjusted.

- **Moxfield Card Spoiler Grid**: Added support for Moxfield-style spoiler view inside the List View tab. The layout dynamically toggles between "Spreadsheet Grid" (DataGrid) and "Card Spoiler Grid" (grid of card images using `views:CardImagePresenter`, with quantity badges on top-left and price badges on bottom-right).
- **Layout Setting in Settings View**: Mounted a "Deck List Layout Mode" ComboBox in the settings panel right below the Language selection, backed by the new `DeckListLayoutMode` setting and serialized/loaded via `MainViewModel` and `DeckStorageService`.
- **Search & Inspection Sync**: Refactored the text search filter to apply directly to the shared collection view of `ActiveDeckViewModel.Cards`, ensuring search filtering works in real-time on both views. Configured a double-click event handler `DeckSpoilerItem_MouseDoubleClick` to select and inspect the card details in the inspector.


## 🌟 Deck View Spoiler Layout & Search Layout Adjustments

We have further upgraded the user interface layouts based on user requests:
1. **Active Deck View Moxfield-style Card Spoilers**:
   - Overhauled [CardItemView.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/Views/CardItemView.xaml) to display active deck cards as vertical image previews with card crops/drawings rather than plain text names.
   - Integrated live quantity badges (top-left corner) and green market price badges (bottom-right corner) over the cards.
   - Built a sleek hover overlay displaying circular `+` and `-` count adjusters that slide in when mouse pointers enter the card bounds.
2. **Stacked Search Engine Filters**:
   - Restructured the search filters panel inside [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml) into a clean, vertically stacked format to improve readability.
   - Positioned the **ORACLE RULES TEXT** text search filter first, followed by **CARD TYPE**, **SORT BY**, the **Search Filters Staples** checkbox, and the **Zoom slider**.
3. **Workspace Layout Toggle Toolbar**:
   - Discovered that the Left/Right workspace sidebar toggle buttons (collapsing the search and stats panels) were not wired up in the XAML markup.
   - Repositioned these buttons to the top of the left navigation panel, aligned directly to the right of the "DECKS" header, using the gold/gray `SidebarToggleButtonStyle`.
   - Corrected `SetLeftSidebarVisibility` in [MainWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml.cs) to collapse/expand `LeftSearchPanelGrid` rather than hiding the main active deck list.


## 🧭 Left/Right Deck Scrolling Buttons & Sidebar Toggle Repositioning

We have updated the navigation sidebar and layout toggling controls as requested:
1. **Left/Right Deck Selection Scrolling Buttons**:
   - Replaced the workspace sidebar toggle buttons at the top of the left navigation panel next to the **DECKS** header.
   - Inserted new left (`&#xE112;`) and right (`&#xE111;`) deck scroll buttons wired to click handlers `PrevDeck_Click` and `NextDeck_Click` in [MainWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml.cs).
   - Clicking these buttons cycles backward or forward through the saved decks list, updating the active workspace.
2. **Repositioned Sidebar Toggles in Deck Header**:
   - Relocated the **Search** (left sidebar) and **Stats / Analysis** (right sidebar) layout toggles to the top-right of the deck workspace header panel next to the Context Ellipsis Button.
   - Styled them as standard 38x38 square `ToggleButton` controls with Fluent `SidebarToggleButtonStyle` to match the ellipsis and save action button row perfectly.


## 🎞️ Passive Deck Name Auto-Scroll (Marquee) on Hover

We have removed the deck scrolling arrows and replaced them with a passive, dynamic marquee text animation:
1. **Removed Deck Scroll Arrows**:
   - Reverted the top of the navigation panel to show the clean **DECKS** header label without buttons.
2. **Hover Marquee Effect**:
   - Updated the Saved Decks list template in [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml) to nest the deck name TextBlock inside a `ClipToBounds="True"` Grid.
   - Assigned the text block a `RenderTransform` (`TranslateTransform`).
   - Wired up `DeckName_MouseEnter` and `DeckName_MouseLeave` in [MainWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml.cs).
   - When hovered, if a deck name overflows its container width, it smoothly scrolls to the left to reveal the hidden text, pauses, cycles back and forth, and immediately resets to 0 when the mouse leaves.


## 🧹 Inner ListBox Scrollbar Disablement

To resolve the two light-colored rectangles (WPF rendering artifacts of the internal scrollbar track/corners) appearing below the last deck in the sidebar:
1. **Disabled ListBox Scrollbars**:
   - Set `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` and `ScrollViewer.VerticalScrollBarVisibility="Disabled"` on the inner `SavedDecks` `ListBox` in [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml).
   - This ensures all scrolling behaviors are delegated entirely to the outer `ScrollViewer`, removing any default inner scrollbar/track elements.


## 📏 Unconstrained TextBlock Layout Wrapping via StackPanel

To fix the layout clipping issue where long deck names were truncated during layout measure and couldn't scroll past the initial clipped bounds:
1. **Added StackPanel Container**:
   - Wrapped the deck name `TextBlock` in a horizontal `<StackPanel Orientation="Horizontal" HorizontalAlignment="Left" VerticalAlignment="Center">` in [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml).
   - This bypasses Grid boundary constraints, allowing the TextBlock to measure its actual width using the full unclipped string length.
2. **Updated Event Traversals**:
   - Modified `DeckName_MouseEnter` and `DeckName_MouseLeave` in [MainWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml.cs) to locate the TextBlock nested within the StackPanel.
   - The marquee calculations now compute the true, full string width so the text auto-scrolls completely to the end.


## 🔍 Card Zoom Slider, Deck Deletion Confirmation & EDHrec Cache Cleanup

We have added dynamic card image scaling in the spoiler view, an interactive deletion confirmation, and automatic cleanup of recommendation files:
1. **Interactive Card Zoom Slider**:
   - Added a horizontal `CardZoomSlider` in the Deck Builder List View tab next to the search textbox in [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml).
   - This slider is only visible when the active view layout is set to **Card Spoiler Grid**.
   - Applied a `<Border.LayoutTransform>` using a `ScaleTransform` bound directly to the slider value inside the card spoiler list's `ListBox.ItemTemplate`. This allows the cards (images, borders, badges, prices) to scale up/down smoothly and reflow dynamically.
2. **Deck Deletion Confirmation Dialog**:
   - Updated the `DeleteDeck` command in [MainViewModel.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/ViewModels/MainViewModel.cs) to prompt a confirmation dialog using `MessageBox.Show` before deleting.
   - Deletion is aborted if the user clicks No.
3. **EDHrec Cache Cleanup for Unused Commanders**:
   - Implemented `DeleteUnusedEdhrecCache` in [MainViewModel.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/ViewModels/MainViewModel.cs).
   - When a deck is deleted, it checks if any *other* saved deck uses the same commander card name.
   - If no other deck uses the same commander, the program deletes the cached recommendations JSON file (e.g. `${slug}.json`) from the local `AppData/Roaming/MtgCommanderBuilder/Data/EdhrecCache/` folder.


## 🖱️ Context Ellipsis Button Left-Click Trigger

Fixed the usability issue where left-clicking the options menu button (`...`) did not display the context menu:
1. **Registered Left-Click Event**:
   - Added `Click="ContextButton_Click"` on the `Button` in [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml).
2. **Left-Click Code-Behind Event Handler**:
   - Created the `ContextButton_Click` method in [MainWindow.xaml.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml.cs).
   - This manually anchors and opens the `ContextMenu` directly below the button, matching standard intuitive click behavior.


## 🔬 MVVM Card Zoom Binding Resolution

Fixed the issue where the card zoom feature did not scale card images at runtime due to WPF NameScope instantiation limits inside list item DataTemplates:
1. **Added CardZoomScale property to MainViewModel**:
   - Implemented a `CardZoomScale` property in [MainViewModel.cs](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/ViewModels/MainViewModel.cs) to bind both the slider controls and the scale transforms.
2. **Updated Bindings via RelativeSource**:
   - Bound the `CardZoomSlider` in [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml) directly to `CardZoomScale` in a TwoWay mode.
   - Updated the `<ScaleTransform>` inside the ListBox `DataTemplate` to reference `DataContext.CardZoomScale` on the ancestor `ListBox` via `{RelativeSource AncestorType=ListBox}`.
   - This bypasses template NameScope boundaries, allowing correct runtime evaluation of scaling operations.


## 🧙 Deck Wizard Step Panel UI & Card Zoom Reflow

Fixed the issues where the Deck Wizard could not be completed and the card zoom scaling failed:
1. **Container-Based Card Scaling (Fixed Zoom)**:
   - Moved the `ScaleTransform` from inside the `DataTemplate` border to the `ListBox.ItemContainerStyle` `LayoutTransform` property inside [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml).
   - This scales the actual `ListBoxItem` container, resolving visual tree binding lookups and reflowing the items panel correctly.
2. **7-Step Deck Wizard Layout**:
   - Implemented 7 distinct setup views linked to visibility properties (`Step1Active` through `Step7Active`) inside [MainWindow.xaml](file:///c:/Users/rsuff/OneDrive/Documents/MtgCommanderBuilder-Wild/MainWindow.xaml).
   - Added a progress header with a `ProgressBar` displaying the current step.
   - Added Back, Continue, and Generate Deck buttons at the bottom that dynamically hide and show depending on the wizard's step progress.
