# Guide to the corkboard

The corkboard is a page where files are pinned as cards, like notes on a board, and placed where you want them: to plan a story by its chapters, or to keep the files you work on in view. Each card shows the title of its file and a number, in a color you give it, and `Enter` opens the file.

It is opened with `:board` (or `:corkboard`) from every command line (the start page, the file browser, the favorites, the history, the hotlist, the search results, the Jump menu and the editor), with **Corkboard** (`C`) on the Start tab of the start page, and with `:jump board` or the Blyant tab of the [Jump menu](Guide-to-jump-menu.md#jump-menu). It opens on top of the page you were on, and `Esc` takes you back there.

## Pinning files
- `:pin` in the editor pins the file being edited, and in the file browser, the favorites, the history and the search results the selected file. The message tells the number of its card: `Pinned to the corkboard (4): Chapter one`. A file is pinned once; pinned again, you are told where it is
- `N` (or `:new`) on the corkboard: pick a file in the file browser to pin

A new card is placed at the first free place, row by row, with a column between the cards; on a full board it is put on top of the others, a step from the last one. Its number is the lowest one that is free, and it keeps it.

## On the corkboard
- `Tab` `Shift+Tab` Select the next/previous card, by number
- `12` and `Enter` Select card 12 (the number is shown on the line above the keys while you type it; `Esc` drops it)
- `h` `j` `k` `l` (or the arrow keys) Select the nearest card that way
- `Enter` Open the file of the selected card in the editor. It is a jump: `Ctrl+K` and `Enter` in the editor take you back to the file you were in
- `V` Move the selected card: `h` `j` `k` `l` (or the arrow keys) move it one step, `H` `J` `K` `L` five steps, and `Esc` (or `Enter`, `V`) ends the moving
- `T` (or `:top`) Put the card on top of the others, `E` (or `:bottom`) below them. Where cards overlap, the one on top covers the others
- `C` (or `:color`) Pick the color of the card, its frame and title, from the 16 colors
- `X` (or `:rename title`) Give the card a title of its own (the file keeps its name). Without one, the card shows the name of its file without the extension
- `D` (or `:remove`) Take the card off the board, after asking. The file stays where it is. `u` (or `:undo`) puts it back
- `/` Filter the cards: the others are dimmed, and `Tab` goes through those that are left. `Esc` shows all again
- `:missing` The cards whose file is not there any more, in a list to select one. `:prune` Remove them, after asking (those on a drive that is not connected stay)
- `Ctrl+O` (or `:jump`) the Jump menu, `Ctrl+K` (or `:recent`) the latest files, `?` (or `:help`) the keys, `Esc` (or `Q`) close

The selected card is drawn on top of the others while it is selected, with its number highlighted; that does not change the order of the cards. A card whose file is gone is dimmed and marked _(missing)_.

## Where the cards are kept
The cards are kept in `board.txt` in the data directory, one line for each: its number, its place (column and row), its color, its own title and the path of its file, with a tab character between them. The cards are drawn in the order of the lines, so the last one is on top.

A card's place is kept as it is placed. In a smaller window, a card that would be outside it is drawn at its edge, and it is back in its place in the larger window. When a file is renamed or moved in Blyant (in the file browser or with `:rename` in the editor), its card follows it.
