# result-item-context-menu Specification

## Purpose
Define how Atlas Storage Analyzer offers a context menu on result items in the
folder tree, the treemap, Largest files, and Duplicates: which item the menu
acts on, how its entries mirror the existing selected-item commands, how Copy
path and Move to Trash behave, and how the menu stays accessible and keeps the
existing cleanup safety rules.

## Requirements

### Requirement: Result items offer a context menu

Atlas Storage Analyzer SHALL open a context menu when the user Control-clicks
or right-clicks an item in the folder tree, the treemap, the Largest files list,
or the Duplicates list of a completed scan result. Opening the context menu on
an item MUST make that item the selected item before the menu is shown, so that
every menu entry acts on the item the user opened the menu on. Opening the menu
where no item is shown MUST NOT open an item context menu and MUST NOT change
the selection.

#### Scenario: Right-clicking a tree item selects it and opens the menu

- **GIVEN** a completed scan result is displayed
- **AND** one item is selected in the folder tree
- **WHEN** the user right-clicks a different item in the folder tree
- **THEN** the right-clicked item becomes the selected item
- **AND** a context menu for that item is shown

#### Scenario: Right-clicking a treemap block selects it

- **GIVEN** a completed scan result is displayed in the treemap
- **WHEN** the user Control-clicks a treemap block
- **THEN** the item of that block becomes the selected item
- **AND** a context menu for that item is shown

#### Scenario: Right-clicking a large file or duplicate entry selects it

- **GIVEN** the Largest files list or the Duplicates list shows an item
- **WHEN** the user right-clicks that item
- **THEN** that item becomes the selected item
- **AND** a context menu for that item is shown

#### Scenario: Right-clicking empty space opens no item menu

- **GIVEN** a completed scan result is displayed
- **AND** an item is selected
- **WHEN** the user right-clicks an area of the treemap that shows no block
- **THEN** no item context menu is shown
- **AND** the selected item is unchanged

### Requirement: Context menu entries mirror the selected-item commands

Atlas Storage Analyzer SHALL offer Quick Look, Show details, Reveal in Finder,
Copy path, Add to cleanup basket, Remove from cleanup basket, and Move to Trash
in the item context menu. Each entry that has a toolbar or keyboard equivalent
MUST be enabled exactly when that equivalent is enabled for the same selected
item and MUST invoke the same behavior. Opening an entry from the context menu
MUST NOT bypass any check, confirmation, or status message that the
equivalent command performs.

#### Scenario: Basket entries follow basket membership

- **GIVEN** a selected item is not in the cleanup basket
- **WHEN** the user opens its context menu
- **THEN** Add to cleanup basket is enabled
- **AND** Remove from cleanup basket is disabled

#### Scenario: Basket entry for an item already in the basket

- **GIVEN** a selected item is in the cleanup basket
- **WHEN** the user opens its context menu
- **THEN** Remove from cleanup basket is enabled
- **AND** Add to cleanup basket stays enabled exactly as the toolbar's Add to
  basket command does
- **AND** choosing it reports that the item is already in the cleanup basket
  without adding it again

#### Scenario: Entries are disabled while an update restart is pending

- **GIVEN** an update restart is pending
- **WHEN** the user opens an item context menu
- **THEN** every entry whose toolbar equivalent is disabled during the pending
  restart is disabled in the menu

#### Scenario: Entries are unavailable while a scan replaces the result

- **GIVEN** an item context menu is open
- **WHEN** a new scan starts and the displayed result is cleared
- **THEN** the context menu closes
- **AND** no entry acts on an item from the replaced result

### Requirement: Move to Trash from the context menu stays recoverable

Atlas Storage Analyzer SHALL apply the same protected-path classification and
the same Trash confirmation to Move to Trash invoked from an item context menu
as to the toolbar command. A protected item MUST NOT be moved to Trash and MUST
produce the existing user-visible reason. An eligible item MUST be moved to
macOS Trash only after the user confirms, and MUST NOT be permanently deleted.

#### Scenario: Protected item is blocked from the context menu

- **GIVEN** the scan root is shown in the folder tree
- **WHEN** the user opens its context menu and chooses Move to Trash
- **THEN** Atlas Storage Analyzer does not ask for Trash confirmation
- **AND** it does not invoke the platform Trash service
- **AND** it explains that the scan root is protected from cleanup

#### Scenario: Eligible file asks for confirmation

- **GIVEN** an eligible ordinary file is shown in the Largest files list
- **WHEN** the user opens its context menu and chooses Move to Trash
- **THEN** Atlas Storage Analyzer asks for Trash confirmation
- **AND** it moves the file to macOS Trash only after confirmation succeeds

#### Scenario: Declining confirmation leaves the item in place

- **GIVEN** the Trash confirmation for a context-menu Move to Trash is shown
- **WHEN** the user cancels the confirmation
- **THEN** the item is not moved
- **AND** the displayed scan result is unchanged

### Requirement: The user can copy an item's path

Atlas Storage Analyzer SHALL copy the full path of the selected item to the
clipboard as plain text when the user chooses Copy path from the item context
menu or presses Command-Option-C while a result item is selected and no text
field has focus. Copy path MUST be disabled when no item is selected, MUST NOT
read file contents, and MUST NOT send the path anywhere other than the local
clipboard.

#### Scenario: Copying a path from the context menu

- **GIVEN** a file is shown in the folder tree
- **WHEN** the user opens its context menu and chooses Copy path
- **THEN** the clipboard contains exactly the file's full path as plain text

#### Scenario: Copying a path with the keyboard

- **GIVEN** a directory is selected in the treemap
- **AND** no text field has focus
- **WHEN** the user presses Command-Option-C
- **THEN** the clipboard contains exactly the directory's full path

#### Scenario: Shortcut in a text field keeps text editing behavior

- **GIVEN** an item is selected
- **AND** the search field has focus
- **WHEN** the user presses Command-Option-C
- **THEN** Atlas Storage Analyzer does not replace the clipboard with the item path

#### Scenario: Copy path without a selection

- **GIVEN** no item is selected
- **WHEN** the user presses Command-Option-C
- **THEN** the clipboard is unchanged

### Requirement: Context menu entries are accessible and have equivalents

Atlas Storage Analyzer SHALL give every item context-menu entry an accessible
name that states its action, and SHALL keep every entry reachable without the
context menu through a toolbar command or a keyboard shortcut. Entries with a
keyboard shortcut MUST show that shortcut in the menu.

#### Scenario: Entries expose accessible names

- **GIVEN** an item context menu is open
- **WHEN** assistive technology reads the menu
- **THEN** each entry is announced with a name that states its action

#### Scenario: Actions remain available without a pointer

- **GIVEN** an item is selected using only the keyboard
- **WHEN** the user wants any action offered by the item context menu
- **THEN** that action is available through the toolbar or a keyboard shortcut
