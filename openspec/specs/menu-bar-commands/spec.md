# menu-bar-commands Specification

## Purpose
Define how Atlas Storage Analyzer exposes its commands in the native macOS menu
bar: which menus and entries exist, how each entry mirrors the enablement and
behavior of its command, which keyboard shortcuts apply, how shortcuts avoid
overriding text editing, and how destructive commands stay confirmed.

## Requirements

### Requirement: The menu bar exposes the application's commands

Atlas Storage Analyzer SHALL present File, Edit, Scan, Item, View, Window, and
Help menus in the macOS menu bar while the main window is active. These sit
next to the application menu. Every scan, selected-item, export, cleanup-basket,
history, and scan-option command available in the main window MUST be reachable
from one of these menus.

#### Scenario: Commands are discoverable in the menu bar

- **GIVEN** the main window is active
- **WHEN** the user opens the Item menu
- **THEN** it lists Quick Look, Show Details, Reveal in Finder, the cleanup
  basket action, and Move to Trash

#### Scenario: Update entries stay in the application menu

- **GIVEN** a direct-download build
- **WHEN** the user opens the application menu
- **THEN** it still offers Check for Updates and Automatically Check for Updates

#### Scenario: The App Store build has no update entries

- **GIVEN** the Mac App Store build
- **WHEN** the user opens any menu
- **THEN** no update entry is shown

### Requirement: Menu entries mirror their commands

Atlas Storage Analyzer SHALL make each menu entry invoke the same command as its
toolbar, context-menu, or panel counterpart, with the same data. Each entry MUST
be enabled exactly when that command can execute. Entries whose action depends
on state MUST show the label of the action they will perform.

#### Scenario: A disabled command is a disabled entry

- **GIVEN** no item is selected
- **WHEN** the user opens the Item menu
- **THEN** every selected-item entry is disabled

#### Scenario: The basket entry names its action

- **GIVEN** the selected item is already in the cleanup basket
- **WHEN** the user opens the Item menu
- **THEN** the basket entry reads Remove from Cleanup Basket

#### Scenario: Scan and Stop follow the scan state

- **GIVEN** a scan is running
- **WHEN** the user opens the Scan menu
- **THEN** Stop is enabled
- **AND** Scan is disabled

### Requirement: Commands have macOS keyboard shortcuts

Atlas Storage Analyzer SHALL provide these shortcuts: Command-O Select Folder,
Command-E Export as CSV, Shift-Command-E Export as JSON, Command-R Scan or
Rescan, Command-Period Stop, Command-Y and Space Quick Look, Command-I Show
Details, Option-Command-R Reveal in Finder, Command-B add to or remove from the
cleanup basket, Command-Delete Move to Trash, Command-F Filter and Search, and
Command-M Minimize. A shortcut MUST do nothing when its command cannot execute.

#### Scenario: Command-R rescans the selected location

- **GIVEN** a folder is selected for scanning and no scan is running
- **WHEN** the user presses Command-R
- **THEN** Atlas Storage Analyzer scans that folder

#### Scenario: Command-Period stops a running scan

- **GIVEN** a scan is running
- **WHEN** the user presses Command-Period
- **THEN** the scan stops and keeps its partial-result behavior

#### Scenario: A shortcut for an unavailable command does nothing

- **GIVEN** no scan result is displayed
- **WHEN** the user presses Command-E
- **THEN** no export is started

### Requirement: Menu entries show only shortcuts the menu can own

Atlas Storage Analyzer SHALL show each shortcut that does not collide with text
editing in its menu entry. Space, Command-B, and Command-Delete MUST NOT be
registered on the menu, because a registered menu shortcut reaches the menu
before a focused text field. Their entries show no shortcut, and the item
context menu shows them instead.

#### Scenario: A registered shortcut is shown in its menu entry

- **GIVEN** the main window is active
- **WHEN** the user opens the Item menu
- **THEN** Quick Look shows Command-Y and Show Details shows Command-I

#### Scenario: A colliding shortcut is not registered on the menu

- **GIVEN** the main window is active
- **WHEN** the user opens the Item menu
- **THEN** the cleanup basket entry and Move to Trash show no shortcut
- **AND** Command-B and Command-Delete still act on the selected item while no
  text field has focus

### Requirement: Item shortcuts never override text editing

Atlas Storage Analyzer SHALL NOT perform a scanned-item action for Space,
Command-Delete, Option-Command-C, or Command-B while a text field has keyboard
focus. The key MUST keep its text-editing behavior.

#### Scenario: Command-Delete in the search field edits text

- **GIVEN** the search field has focus and contains text
- **AND** an item is selected
- **WHEN** the user presses Command-Delete
- **THEN** the search text is edited
- **AND** no Trash confirmation is shown

### Requirement: Destructive menu commands stay confirmed and recoverable

Atlas Storage Analyzer SHALL apply the existing protected-path check and Trash
confirmation to Move to Trash invoked from the menu or its shortcut, and MUST
only move items to macOS Trash.

#### Scenario: Move to Trash from the menu asks first

- **GIVEN** an unprotected item is selected
- **WHEN** the user chooses Item > Move to Trash
- **THEN** Atlas Storage Analyzer asks for confirmation before moving it to Trash

#### Scenario: A protected item is refused from the menu

- **GIVEN** a protected item is selected
- **WHEN** the user presses Command-Delete
- **THEN** Atlas Storage Analyzer explains why the item is protected
- **AND** nothing is moved

### Requirement: Recent locations are available from the File menu

Atlas Storage Analyzer SHALL list the recent scan locations in a File > Open
Recent submenu in the same order as the start screen. Choosing one MUST behave
like choosing it on the start screen. The submenu MUST offer Clear Menu, which
clears recent locations exactly as the start screen's clear action does.

#### Scenario: Scanning a recent location from the menu

- **GIVEN** recent locations exist
- **WHEN** the user chooses one from File > Open Recent
- **THEN** Atlas Storage Analyzer scans that location

#### Scenario: A missing recent location is removed

- **GIVEN** a recent location no longer exists
- **WHEN** the user chooses it from File > Open Recent
- **THEN** it is removed from recent locations with the existing notice

### Requirement: Text fields have a standard Edit menu

Atlas Storage Analyzer SHALL provide an Edit menu with Undo, Redo, Cut, Copy,
Paste, and Select All that act on the focused text field. The standard macOS
shortcuts for these commands MUST keep working in the focused text field and
MUST NOT be registered on the menu, so their entries show no shortcut.

#### Scenario: Pasting into the search field

- **GIVEN** the search field has focus
- **WHEN** the user chooses Edit > Paste
- **THEN** the clipboard text is inserted into the search field

#### Scenario: Pasting with the keyboard keeps text editing behavior

- **GIVEN** the search field has focus
- **WHEN** the user presses Command-V
- **THEN** the clipboard text is inserted into the search field once
