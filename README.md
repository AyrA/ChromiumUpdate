# ChromiumUpdate

This application keeps chromium updated.
Chromium (as opposed to Chrome) has no automatic updates.

# Chromium vs Chrome

Chromium is the most widely used base component to display web pages.
It's fully functional on its own but is better known by the browser that builds on top of it:
Google Chrome

Chromium is the part of the browser and engine that is open source.
It's free for anyone to obtain, modify and redistribute.

# How to use

You can launch this application by double clicking.
It will present you with a tiny user interface that allows you to install the updater and Chromium.
If the application detects an installed Chromium and/or updater, you can uninstall them too.

# Update conditions

Unless `/force` is specified, updates are only searched after at least 24 hours have passed.
An update is only installed if the reported installer version is larger than the installed Chromium version.

# Command line arguments

This application supports various command line arguments:

- /force: forces installation of chromium even if there is no new version available.
- /update: launches the application in update mode even if no configuration is available.

# License

This product is in no way associated with Google Inc. and/or the Chromium project/developers.
This application makes use of the Chromium icon.
[The Chromium license](https://github.com/chromium/chromium/blob/master/LICENSE) applies to it.

# TODO

- [ ] Keep updater running after an update and periodically check again (currently it exits)
- [ ] Implement portable mode
- [ ] Inform the user if they need to restart the browser.
