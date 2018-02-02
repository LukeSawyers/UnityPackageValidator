# UnityPackageValidator
A utility to help resolve dependencies between directories within a Unity Project that are meant to be exported as unity packages

Licensed under the GNU Lesser General Public License v3 (LGPL-3.0)

## Usage:

This editor script creates an editor window from which unity packages in the repository can be evaluated for interdependencies. this window can be found under "Tools"

### Package Manifest  

This editor script will search within the repository for a file named UnityPackageManifest.txt. This file tells the script which folders are intended to be exported as unity packages.

The contents of this file should look something like this:

Packages:

FolderOne

FolderTwo

Dependencies:

FolderTwo FolderOne

This tells the validator to look for root folders called FolderOne and FolderTwo and these are intended to be exported as unity packages.

This also tells the validator that FolderTwo is intended to be dependent on FolderOne.

The package manifest can be edited and saved from the editor window

### Validation

Clicking "Validate Packages" will cause the dependencies to be collected in each of the folders specified and each file directory is checked.

Each package found will be listed in the editor window with a dropdown menu.

If a dependent file is found outside of the package folder and not within a folder that is listed as a dependency, it's full path from Assets/ will be listed under External Dependencies within the dropdown menu

All packages that that package is dependent on will be listed under Package Dependencies

### Exporting

Once found Each package can be individually exported by clicking on Export Package

All packages can be exported by clicking Export All Packages at the bottom of the menu window. 