# Mobile Item Controls

The Android and OpenHarmony native clients use the same item gestures. Normal
keyboard, mouse and physical-controller controls remain available.

## Normal Inventory

- Tap an item to inspect its details without picking it up.
- Hold an item still for half a second to use a usable item or learn its skill.
  An item that cannot be used does not fall back to equipping, selling or dropping.
- To move an item, press and immediately drag it to another normal-inventory
  square. Base and available extension grids accept placement.
- Moving six reference-canvas units starts a drag and cancels the hold-to-use
  gesture. Timing is elapsed time, independent of the chosen frame limit.
- Releasing outside the normal inventory restores an unsent drag. A second
  finger, focus loss, viewport change, a modal dialog, repair/lock state or
  a panel change also cancels it. A server replacement is never overwritten.

This gesture is not a shortcut for shop selling, trading, storage, crafting or
the equipped-item slots. Those workflows retain their existing controls.

## Potion Slots

Tap one of the four bottom potion slots to use its current Q/W/E/R item. Empty
slots do nothing. A prolonged hold does not repeatedly use potions. Existing
item and buff checks are shared with the keyboard quick slots. Bottom-bar and
inventory touches cannot cast a world skill or activate multi-finger world
commands during the same contact sequence.
The always-visible character names, event status and collapsed skill bar do not
disable movement or inventory gestures. Opening the skill selection panel
temporarily gives its choices priority over world and inventory gestures.

## Character Selection

Tap a character, then the enter button at the bottom right. Entering does not
require a mobile world-skill double tap or a hardware keyboard. The bottom bar
stays within the reference canvas at desktop and mobile drawable sizes. Name,
guild and class labels are fitted into the available spacing between live
character slots.

## Dialog Buttons

Tap a dialog button once to confirm; no prior hover or second tap is required.
Fast presses and releases received between game frames still form one click.
Releasing outside the dialog, adding another finger or losing app focus cancels
an unfinished touch. A newly opened dialog cannot inherit the click that opened
or replaced it. Desktop mouse and controller dialog input remain available.

## Verification Status

As of 2026-09-27, focused native tests exercise the touch arbitration, first-touch
selection, real inventory touch methods, item identity checks and restore
decisions, plus window and label geometry. Rendering, item-manager and packet
dependencies in the inventory test use local doubles. Source-integration
contracts supplement these tests; they do not replace runtime UI acceptance.

Dialog regressions compile the actual message-box manager with headless rendering
and key-polling doubles. They exercise first contact, coalesced edges, cancellation,
captured coordinates and popup-address reuse. New Android dialog behavior still
needs emulator replay after the native library and APK are rebuilt.

Actual emulator consumption, skill learning, dragging, interruption recovery and
the corrected character-selection screenshot still require recorded acceptance.
Successful compilation or a mapper test alone does not establish those results.
