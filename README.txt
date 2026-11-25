=====================================================================
  D E S K O S A U R  -  a desktop platformer            v1.2
=====================================================================

A little pixel dino runs around ON your actual Windows desktop.
Beat 20 levels of platforms, ice, fans, springs and portals... and
use your own desktop as extra platforms: icons, window title bars,
Chrome text boxes, buttons, Explorer files, anything with an edge.

---------------------------------------------------------------------
 RUN IT
---------------------------------------------------------------------
  1. Unzip the whole folder.
  2. Double-click "Install Deskosaur.bat"  (no admin needed)
  3. Press the Windows key, type Deskosaur, hit Enter. That's it.

  The installer puts the game in %LOCALAPPDATA%\Programs\Deskosaur,
  adds a Start menu shortcut (optionally a desktop one too), and lists
  it in Settings > Apps so you can uninstall it like any other app.
  Re-run the installer from a newer zip to update - your save is kept.

  (You can also just double-click Deskosaur.exe without installing.)

  Needs Windows 10 or 11 (uses the .NET Framework 4.8 that ships with
  Windows, so there's nothing to install).

  Windows SmartScreen may say "Windows protected your PC" because the
  exe isn't code-signed. Click "More info" -> "Run anyway".
  (Some antivirus tools also get nervous about the keyboard hook the
  game uses to read your keys while other windows have focus. It only
  runs while you're playing a level and is removed on exit.)

---------------------------------------------------------------------
 CONTROLS (in a level)
---------------------------------------------------------------------
  Left / Right  or  A / D    run
  Space / Up / W             jump  (press again in mid-air = double jump)
  Down / S                   drop through planks
  R                          restart level
  Esc  or  P                 pause  (frees your keyboard so you can type)

  While a level is running the overlay is fully click-through, so you
  can drag windows around with the mouse. Drag a window under the dino
  to build a bridge - it rides along when you move it!

---------------------------------------------------------------------
 YOUR DESKTOP IS PART OF THE LEVEL
---------------------------------------------------------------------
  Glowing lines show where the dino can land:
     teal   = window title bars / window tops
     yellow = desktop icons (files & shortcuts)
     pink   = things inside windows: text boxes, buttons, tabs,
              links, images, Explorer files...
  Every level is beatable WITHOUT any of this (checked by a solver
  bot that plays each level with the real game physics) - your
  desktop just gives you shortcuts.

  Options -> DESKTOP PLATFORMS turns this off, SHOW DESKTOP EDGES
  hides the glowing hints.

---------------------------------------------------------------------
 STUFF IN THE GAME
---------------------------------------------------------------------
  4 dinos: REX, BRONTO, STEGGY, TRIKE
  Wardrobe: 7 hats, 7 glasses, 8 shirts, 6 pairs of shoes (rocket boots shoot
  flames, the propeller hat spins). Fancy items unlock with AMBER -
  every level hides 3 pieces (60 total).

  Worlds: NEON ALLEY / FROST BYTE / FAN FACTORY / EVENT HORIZON
  Mechanics: double jump, one-way planks, spikes, checkpoints, springs,
  slippery ice (with skid sparkles), crumbling blocks, jump-refill
  orbs, up-drafts and side fans (tail-wind boost / head-wind slowdown),
  conveyor belts, moving platforms, portals... and a black-hole exit
  that catches you, holds you up, and spaghettifies you (cutely).

---------------------------------------------------------------------
 SAVE FILE
---------------------------------------------------------------------
  %APPDATA%\Deskosaur\save.txt   (progress, times, amber, outfit,
                                  options - plain text)
  Crash details, if any:  %APPDATA%\Deskosaur\crash.log

---------------------------------------------------------------------
 BUILD FROM SOURCE
---------------------------------------------------------------------
  Double-click build.bat. It uses the C# compiler that's already part
  of Windows (C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe),
  so no Visual Studio needed. Code is plain C# 5 + WinForms.

  src/Core   engine: software renderer, pixel art, physics, levels,
             particles, launcher, chiptune synth (platform independent)
  src/Win    Windows bits: transparent overlay, keyboard hook,
             waveOut audio, desktop scanner (windows + icons + UI
             Automation for in-window elements)
  tools      dev tools (level solver bot, preview renderers)

  Levels are ASCII maps in src/Core/Levels.cs - the legend is at the
  top of that file if you want to make your own.
