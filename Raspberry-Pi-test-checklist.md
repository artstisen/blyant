# Testing Blyant on a Raspberry Pi

What to try on the Pi, and what should happen. Try it in both places where it behaves differently:

- **The text console**: no desktop, or `Ctrl+Alt+F2` from the desktop (`Ctrl+Alt+F7` or `F1` goes back). There `TERM=linux`: 8 colors, no clipboard.
- **A desktop terminal** (LXTerminal), and if you can, over SSH from Windows Terminal.

Write down what differs, and copy `data/log.txt` afterwards: it tells what Blyant did with files (never what is in them).

## 1. Which build, and starting

- [ ] `uname -m`: `aarch64` is the 64-bit Pi OS (use `Blyant-1.1.1-linux-arm64.tar.gz`), `armv7l` the 32-bit one (`linux-arm`)
- [ ] `tar -xzf Blyant-1.1.1-linux-arm64.tar.gz`, then `cd Blyant` and `./Blyant --version`: prints `Blyant v1.1.1`
- [ ] `./Blyant --help | less`: the usage, also through a pipe
- [ ] `./Blyant`: the start page. The `data` folder is made next to the program (`ls`)
- [ ] The window must be at least **80 columns and 24 rows**. LXTerminal opens at 80x24, which is enough: every page fits, and nothing is drawn in the last column or on the last row
- [ ] In the text console the size comes from the screen: at 1920x1080 it is large; at 640x480 it is 80 columns, which is enough

## 2. Colors (text console)

- [ ] The dividers, the frames of the tabs, the borders of boxes and the line numbers are **gray**, not black (dark gray used to turn black there)
- [ ] White and gray text differ (headings are brighter than text)
- [ ] Try the color schemes _Gray Box_ and _Bright Typist_ in Configuration: the text can be read, and selected text can be seen
- [ ] _Black and white_ on: headings still stand out

## 3. Keys

- [ ] Arrows, `Home`, `End`, `PgUp`, `PgDn`, `Delete`, `Backspace`, `Enter`, `Tab`, `Shift+Tab` and `Esc` on the start page, in the file browser and in the editor
- [ ] In the editor: `i`, type, then `Esc` and `j` quickly: the cursor goes down, no "j" is typed (Esc and a key that come together are now split)
- [ ] `Ctrl+R` (redo), `Ctrl+O` (Jump menu), `Ctrl+K` (snippets), `Ctrl+D` / `Ctrl+U` (half page)
- [ ] `Ctrl+C` in the editor: a message tells how to close; Blyant does not stop
- [ ] `Ctrl+\`: Blyant does not stop (it used to stop at once)
- [ ] `Ctrl+Z`, then `fg`: Blyant comes back and the page is drawn again by itself, without a key: try it in the editor, the file browser, the start page and the Jump menu
- [ ] In the file browser, `V` to peek at a file, `Tab` into the peek, then `Esc` and `j` quickly: the peek closes and the selection moves down (the `j` is not lost)
- [ ] Keys with `AltGr` (on a keyboard that has it): `@ { } [ ] \ |` are typed as text

## 4. The window

- [ ] Make the desktop terminal smaller than 80 columns: a box asks to make it larger, and nothing is drawn past the edge (on every page, not only the editor)
- [ ] Make it larger again: the page follows

## 5. Files (SD card, and a USB stick)

- [ ] New file, save (`:w`), rename (`X`), copy (`C`), move (`M`) in the file browser
- [ ] Delete (`D`) on the SD card: the file is in the trash (`:trash`). Put it back
- [ ] Delete a **folder on a USB stick**: it is copied to the trash on the SD card, then removed from the stick. With a stick that is FAT or exFAT too
- [ ] On a FAT or exFAT stick: a file named `a:b.md` (made on the Pi, in a folder on the SD card) can be copied there? Delete one with such a name: it goes to the trash
- [ ] `chmod -w notes.md`, open it in Blyant, change it and `:w`: refused with "notes.md is read only: it is not written". `chmod +w` and save again: works, and the file keeps its permissions (`ls -l`)
- [ ] A file of another user (`sudo touch /tmp/root.md`, 0644): saving it is refused, not replaced
- [ ] A file on a FAT stick saves (its permissions cannot be set there, and that must not stop the save)
- [ ] Start Blyant in a folder that has a `data` folder of something else (`mkdir -p ~/site/data; cd ~/site; ~/Blyant/Blyant`): Blyant does not write its settings there (`ls ~/site/data` stays empty)

## 6. Recovery copies

- [ ] Change a file, wait 3 seconds, then close the terminal window (desktop) or the SSH session. Start Blyant and open the file: "has changes that were not saved" is offered
- [ ] Choose _Decide later_, change the file again, close the window again: next time both texts are in **Recover unsaved text** on the start page, and the newest is offered when the file is opened
- [ ] Change a file and at once type `:q` (the "Save changes?" box), then close the window while the box is open: the copy has the last change
- [ ] Do **not** run Blyant with `sudo`. If you did once (the `data` folder then belongs to root), Blyant run as yourself uses `~/.local/share/Blyant/data` instead, and still saves its settings and copies

## 7. Clipboard

- [ ] `yy` in the **text console**: the message says "This terminal may not take the clipboard: use the paste bin (:clipboard)"
- [ ] `:clipboard`, turn the paste bin on, `yy`: the line is in `data/paste-bin.txt`
- [ ] Over SSH from **Windows Terminal**: `yy` puts the line on the Windows clipboard (OSC 52), and the message says "Copied to the clipboard"
- [ ] A very long yank (`ggyG` in a large file): not sent to the terminal, the message tells why
- [ ] If you have kitty, WezTerm or Alacritty: start it from LXTerminal and run Blyant in it. `yy` says "Copied to the clipboard" (not "may not take the clipboard"); the same in tmux

## 8. Speed

- [ ] `?` (the help) over SSH: it draws without a long wait
- [ ] A large text file (several MB): scrolling, `G`, `/search` and `:%s/a/b/g` are quick

## Not known yet (please look)

- What happens when the terminal is closed while Blyant waits for a key: does it stop at once, or keep running (`ps aux | grep Blyant` after closing the window)?
- Moving a file that only you may read (`chmod 600`) to the trash across to a FAT stick, or copying it there: does it work?
