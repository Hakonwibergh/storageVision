# Changelog

## 1.0.0
- Highlight mode `Both`: while picking an online order, only the box the order needs is outlined. Looking at other boxes no longer moves the outline away from it. Without an active order, the looked-at box is outlined as before.

## 0.1.2
- The background box now fits the visible content tightly (no empty space around the product name). New setting: `BackgroundPadding`.
- New setting `ProductTextScale`: size of the product name and brand in the game's panel.
- Settings menu: clearer names, a short explanation under every setting, and no sideways scrolling.
- The box amount text (`Storage → Enabled`) is now off by default. The box outline works without it.

## 0.1.1
- Fixed the box info text overlapping the game's product panel. The position now follows the panel's real size.
- Added one shared dark, semi-transparent backdrop behind the game's panel and the info text for better readability. The opacity is set with `BackgroundOpacity`.
- New in-game settings menu (F8). Every setting can be changed live with toggles, sliders and color swatches. The key is set with `MenuKey`.
- README: how to edit the config in the mod manager.

## 0.1.0
- First release.
- Center HUD under the crosshair. The game's product panel is moved to the center, with box amount and prices added below it.
- Outline on the looked-at storage box, plus an optional order-target mode with an option to hide the game's arrows.
- Packing table info: assigned order, items left, value (payout range, market or shelf price) and the product list.
- Config options for all features.
