# Controls

Solar Majesty is a mouse-and-keyboard game first. A controller can drive the camera, game speed and the main menus, but it cannot point and click (see [Controller limits](#controller-limits)).

Most keys do nothing while you are typing flag orders.

## Camera

| Action | Keyboard / mouse | Controller |
|---|---|---|
| Pan | `W` `A` `S` `D`, or push the pointer to a screen edge (Settings → EDGE SCROLL) | Left stick, or D-pad |
| Zoom out / in | `Q` / `E`, or the mouse wheel (zooms toward the cursor; the wheel scrolls a panel when the pointer is over one) | Left trigger / right trigger, or right stick down / up |
| Orbit | Hold middle mouse and drag (yaw, plus a little pitch) | Right stick left / right (yaw) |
| Focus Campus A / B | `F6` / `F7` | — |
| Jump to most urgent alert | `Backspace` | `A` |

Settings → INVERT CAMERA PAN flips the pan direction for keys, edge scroll and the stick alike.

### Scripted camera beats

The camera takes over for two short moments:

- **Launch** (the **TO …** button on the victory banner): the camera frames the departure craft, follows it up as it lifts off, then returns before the next world loads. About 4.6 s.
- **Victory** (the moment a world's goals are met): a slow pull-back and part orbit over the Commons behind the victory banner. About 7.5 s.

Any key, mouse button, wheel turn, edge scroll or controller input (after the first 0.3 s) ends the beat at once and gives you back exactly the view you had. Pausing also ends it. **Settings → REDUCE MOTION skips both beats.** With Reduce Motion on, the launch goes straight to the next world as it always did.

## Game speed and menus

| Action | Keyboard / mouse | Controller |
|---|---|---|
| Hold / resume the world (HUD and camera stay live) | `Space` | `X` |
| Slower / faster | `,` / `.` | `LB` / `RB` |
| Pause menu (or close Settings, research, or a placement tool first) | `Esc` | `Start` (always opens or closes the pause menu; closes Settings) |
| Back out of research or a placement tool | `Esc` | `B` |
| Research panel | `T` | `Y` |

## Tools

| Action | Keyboard / mouse |
|---|---|
| Cycle Flag → Build → none | `Tab` |
| Build tool / Flag tool / Orbital tool | `B` / `G` / `N` |
| Pick a building from the visible catalog row | `1` … `9`, `0` |
| Place a building | Left click (on release). Hold `Shift` to keep placing. |
| Pick a flag type: Explore, Clear Threat, Build, Extract, Defend | `F1` … `F5` |
| Pick a flag type: Research Site, Outpost, Terraform | `I` / `O` / `U` |
| Raise / lower the selected flag's bounty | `=` or keypad `+` / `-` or keypad `-` |
| Post a flag / select | Left click |
| Cancel the flag under the cursor | Right click |
| Form a party from the selection / disband it | `P` / `[` |
| Talk to the selected hero | `Enter` |
| Map overlay: off → danger → coverage | `V` |

## Banners and dialogs

| Action | Keys |
|---|---|
| Dismiss a cutscene card | `Space` or `Enter` |
| Keep overseeing after a win | `Y` |
| Pay the Fobot Yard to revive a downed party | `Y` |

## Debug and developer

| Action | Keys |
|---|---|
| Debug HUD | `F8` |
| Seed a Campus B explore bounty | `F9` |
| Next world (debug) / unlock the campaign and cycle | `F10` / `Shift+F10` |
| Fatigue every robot (debug) | `R` |
| Play a locked world from the title screen | `Shift` + click |

## Rebinding keys

Settings → **CONTROLS** lists every keyboard action by group, each with its current key.

- **To rebind:** click a row, then press the new key. **Esc** cancels.
- **A key already in use is refused,** and the screen names the action using it. So you can never end up with two actions on one key, or an action with no key.
- **RESET TO DEFAULTS** puts every key back.
- **Gameplay hotkeys stand down** while a rebind waits for its key, just as they do while you type flag orders.
- **The keys shown on the HUD** (tool buttons, flag list, banners) are always your current bindings.

Some keys are fixed on purpose and can't be rebound or taken by another action:

| Key | Why it's fixed |
|---|---|
| **Esc** | The universal back key. It opens the pause menu, closes panels, leaves text boxes and cancels a rebind. |
| **Shift** | The modifier: keep placing after a build, add to a selection, open a locked world from the title screen. |
| **Mouse buttons** | Clicks, not hotkeys. |
| **Enter / Esc in text boxes and dialogs** | They confirm or cancel the box itself. |

**Keypad twins:** Keypad + and −, and Keypad Enter, always work alongside their actions (bounty up/down and Confirm), whatever those are bound to. They count as taken.

**Saved bindings:** bindings are stored per action in PlayerPrefs (`SM_Bindings`).
- **Actions added in later versions** start on their default key.
- **If you had already given that default key to something else,** the new action starts unbound (shown as —) rather than firing twice.
- **The controller layout** is listed read-only at the bottom of the screen; see [Controller](#controller).

## Controller

Any XInput controller (Xbox and compatible pads) works on Windows. Controller support is on by default, and nothing is read until a controller is plugged in. Settings → **CONTROLLER** turns it off, for example when a drifting or stray device keeps nudging the map. The chip also reports **NONE CONNECTED** or **NOT SET UP** (the Input Manager axes are missing).

Feel: sticks have a radial deadzone (20% of travel), and the rest of the travel follows a curve, so a small tilt moves the camera slowly and a full tilt moves it at full speed. Diagonals are not faster than straight lines. Pan speed scales with zoom, as with the keys.

| Button | Action |
|---|---|
| Left stick / D-pad | Pan |
| Right stick X | Orbit |
| Right stick Y, LT / RT | Zoom out / in |
| `A` | Jump to the most urgent alert |
| `B` | Back out of research or a placement tool |
| `X` | Hold / resume the world |
| `Y` | Research panel |
| `LB` / `RB` | Slower / faster |
| `Start` | Pause menu |
| `Back` | Reserved (unassigned) |

### How it is wired

The game uses Unity's **legacy Input Manager** (`activeInputHandler: 0`, no Input System package). The controller entries in `ProjectSettings/InputManager.asset` all start with `SM_Pad`, so they can't collide with Unity's default axes:

| Name | Input Manager source (zero-based axis numbers as stored in the file) |
|---|---|
| `SM_PadLX`, `SM_PadLY` | Joystick axis 0 (X), axis 1 (Y, inverted so up is positive) |
| `SM_PadRX`, `SM_PadRY` | Axis 3 (4th axis), axis 4 (5th axis, inverted) |
| `SM_PadLT`, `SM_PadRT` | Axis 8 (9th axis), axis 9 (10th axis), each 0..1 |
| `SM_PadDX`, `SM_PadDY` | Axis 5 (6th axis), axis 6 (7th axis) |
| `SM_PadA` `B` `X` `Y` | `joystick button 0` `1` `2` `3` |
| `SM_PadLB`, `SM_PadRB` | `joystick button 4`, `5` |
| `SM_PadBack`, `SM_PadStart` | `joystick button 6`, `7` |

This is the Xbox layout as Unity's legacy input reports it on **Windows**. macOS and Linux drivers number the axes and buttons differently: triggers and the D-pad in particular may not respond, or may land on other controls. If you need those platforms, edit the axis numbers in **Project Settings → Input Manager**. The code only refers to the `SM_Pad*` names.

Code: `Runtime/Camera/GamepadInput.cs` reads the axes. `GamepadMath.cs` shapes the input (deadzone, curve, trigger and stick zoom, shoulder buttons) and is unit tested. `IsometricCameraController.cs` applies it. On first use, every `SM_Pad*` name is probed once. If any is missing, controller support switches off for the session with one warning instead of throwing every frame. Nothing is read in batch mode.

### Controller limits

The legacy Input Manager cannot move the OS cursor, and the HUD is drawn with IMGUI, which has no controller focus navigation. So a controller **cannot** place buildings, post or pick flags, select robots, click HUD buttons (the victory banner, the pause menu entries, Settings chips), or use the title screen's world picker. Use the mouse for those. A virtual cursor or full UI navigation would need either the Input System package or the UI Toolkit migration, and neither is in scope for this release.
