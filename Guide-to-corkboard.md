# Guide to the corkboard

The corkboard is a page where files are pinned as cards, like notes on a board, and placed where you want them: to plan a story by its chapters, or to keep the files you work on in view. Each card shows the title of its file and a number, in a color you give it, and `Enter` opens the file. A card can also be a folder (research, scenes) or a note of its own, with a text you write. You can keep several boards, and each [project](Guide-to-start-page.md#projects) can have a board of its own, kept in its folder.

It is opened with `:board` (or `:corkboard`) from every command line (the start page, the file browser, the favorites, the history, the hotlist, the search results, the Jump menu and the editor), with **Corkboard** (`C`) on the Start tab of the start page, and with `:jump board` or the Blyant tab of the [Jump menu](Guide-to-jump-menu.md#jump-menu). It opens on top of the page you were on, and `Esc` takes you back there. `:board name` opens the board with that name (`:board main` the main board).

## Pinning files and folders
- `:pin` in the editor pins the file being edited, and in the file browser, the favorites, the history and the search results the selected file or folder, to the board that is open. The message tells the number of its card: `Pinned to the corkboard (4): Chapter one`. A file or folder is pinned once to a board; pinned again, you are told where it is. One whose path has a tab or a line break in it (Linux and macOS allow them) cannot be pinned
- `N` (or `:new`) on the corkboard: pick a file in the file browser to pin
- `F` (or `:folder`) on the corkboard: pick a folder in the file browser to pin (go to it and press `S`)

The card of a folder has a tab along its top edge, and its name with a `/` after it, as in the file browser:

```
.-///------------------.
| 4  research/         |
'----------------------'
```

`Enter` on it opens the folder in the file browser, the peek shows what it holds, and `I` tells about it as `I` in the file browser does.

A new card is placed at the first free place, row by row, with a column between the cards; on a full board it is put on top of the others, a step from the last one. Its number is the lowest one that is free, and it keeps it.

## Note cards
`A` (or `:note`) adds a note card: its text is written in the editor, and kept when the editor is closed. The first line of the text is the title of the card, unless you give it a title of its own with `X`. `Enter` on a note opens its text in the editor again. A note card has `~` along its top edge, where the card of a file has `-`. A new note left without text is not kept, also when the editor is left by a jump. The text is kept on the board it was written for, also when another board is opened from the editor (`:board name`).

## On the corkboard
- `Tab` `Shift+Tab` Select the next/previous card, by number
- `12` and `Enter` Select card 12 (the number is shown on the line above the keys while you type it; `Esc` drops it)
- `h` `j` `k` `l` (or the arrow keys) Select the nearest card that way
- `Enter` Open the file of the selected card in the editor (a note: edit its text). It is a jump: `Ctrl+K` and `Enter` in the editor take you back to the file you were in
- `V` Move the selected card: `h` `j` `k` `l` (or the arrow keys) move it one step, `H` `J` `K` `L` five steps, and `Esc` (or `Enter`, `V`) ends the moving
- `T` (or `:top`) Put the card on top of the others, `E` (or `:bottom`) below them. Where cards overlap, the one on top covers the others
- `C` (or `:color`) Pick the color of the card, its frame and title, from the 16 colors
- `X` (or `:rename title`) Give the card a title of its own (the file keeps its name). Without one, the card shows the name of its file without the extension, or the first line of a note. The box shows the name the card has: `Enter` on its own name keeps it without a title, so it goes on following its file, and typing its own name takes a title away
- `D` (or `:remove`) Take the card off the board, after asking. The file stays where it is. `u` (or `:undo`) puts it back
- `:clear` Take all the cards off the board, after asking in a box where `No` is selected and a key typed by mistake does not answer. The files stay where they are, and `u` puts the cards back (a card added since keeps its number)
- `I` (or `:info`) Information about the card: for a file, as `I` in the file browser shows it (its size, when it was changed ...), and for a note, its words and lines
- `P` (or `:peek`) The peek on or off: the first lines of the selected card's file (or the text of a note) below the board. It stays on until you turn it off
- `/` Filter the cards: the others are dimmed, and `Tab` goes through those that are left. `Esc` shows all again
- `:missing` The cards whose file is not there any more, in a list to select one. `:prune` Remove them, after asking (those on a drive that is not connected stay)
- `Ctrl+O` (or `:jump`) the Jump menu, `Ctrl+K` (or `:recent`) the latest files, `?` (or `:help`) the keys, `Esc` (or `Q`) close

The selected card is drawn on top of the others while it is selected, with its number highlighted; that does not change the order of the cards. A card whose file is gone is dimmed and marked _(missing)_.

## Arranging the cards
`:arrange` puts the cards in rows from the top left, with a column between them, in an order:
- `:arrange number` by their numbers
- `:arrange name` by their titles, upper and lower case alike, and numbers by their value: _Chapter 2_ comes before _Chapter 10_
- `:arrange color` by their colors, in the order of the color picker, and the cards without a color last

`Tab` completes the orders. The order the cards are drawn in stays, and `u` (or `:undo`) puts the cards back where they were.

## Several boards
`:list` on the corkboard shows the boards, with what is on the selected one beside them: `Enter` opens a board, `N` adds one, `R` renames one and `D` deletes one (to the trash, after asking; the files of its cards stay where they are). The main board is always there; it cannot be renamed or deleted. `:list name` (or `:board name`) opens a board at once, and `Tab` completes the names. `:board` alone shows the boards too.

The board that is open is shown in the top bar and remembered (setting `boardname`), so the corkboard opens on it again and `:pin` pins to it. A name has at most 20 characters.

## The board of a project
`B` (or `:board`) in the list of projects opens the board of the selected project: plan a manuscript by its chapters and scenes, with its research folders beside them. It is kept in the project folder, in the file `.blyant-board.txt`, made when the first card is put on it. To the right of the list of projects, the cards of the selected project's board are shown laid on each other by their numbers, as many as there is room for, with how many there are:

```
Corkboard (5)
.----------------------.
| 1  one               |
'-.----------------------.
  | 2  two               |
  '-.-///------------------.
    | 3  research/         |
    '----------------------'
```

The paths of the files and folders in the project are kept from the project folder (`chapters/one.md`, with `/` on all systems), so the board goes with the project when it is moved, renamed, copied or synced to another computer. A file outside the project is kept with its full path. The boards of the projects are in the box of `:list` after the others, with the name of the project and _(project)_; `:board name` opens one by the name of its project. Such a board has the name of its folder, so `R` does not rename it, and `D` deletes only the file of the board. When the project folder is gone, the main board is opened.

## Where the cards are kept
The cards of the main board are kept in `board.txt` in the data directory, those of the other boards in files with their names in the directory `boards` next to it, and the board of a project in `.blyant-board.txt` in its folder. Each card is one line: its number, its place (column and row), its color, its own title and the path of its file or folder (a folder ends with a separator), with a tab character between them. A note card has no path, and its text is on the line after it. The cards are drawn in the order of the lines, so the last one is on top.

A card's place is kept as it is placed. In a smaller window, a card that would be outside it is drawn at its edge, and it is back in its place in the larger window. When a file or folder is renamed or moved in Blyant (in the file browser or with `:rename` in the editor), its cards follow it, on all the boards, also those of the projects. What `u` puts back is kept while you visit another page, unless the board was changed there.
