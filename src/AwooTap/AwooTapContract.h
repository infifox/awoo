// The contract between awoo and its tray TAP (test automation provider) DLL.
//
// awoo loads this DLL into Explorer through the documented XAML diagnostics
// entry point (InitializeXamlDiagnosticsEx, the same mechanism Visual
// Studio's Live Visual Tree uses). Once inside, the DLL creates a
// message-only window on Explorer's XAML UI thread; awoo drives it by posting
// the window the messages below and reading the LRESULT each returns. That
// window, its class name and these message numbers are the whole surface
// between the two halves, so the C# side (SystemVolumeIconHider) mirrors every
// constant here.
//
// Keep this file in sync with its C# mirror. The protocol is versioned: awoo
// asks for AWOO_TAP_PROTOCOL_VERSION before it trusts the rest, so a mismatch
// between an old DLL and a new awoo (or the reverse) fails cleanly rather than
// talking past itself.

#pragma once

// Bumped whenever the message set or their meanings change. awoo refuses a DLL
// whose WM_AWOO_TAP_PROTOCOL answer is not this number.
#define AWOO_TAP_PROTOCOL_VERSION 1

// The window class of the message-only window the DLL creates inside Explorer.
// awoo finds the window by this class name (FindWindowW) after it injects.
#define AWOO_TAP_WINDOW_CLASS L"Awoo.TrayTap.Control.3117367e"

// The window title, so a stray window of the same class from something else is
// not mistaken for ours.
#define AWOO_TAP_WINDOW_TITLE L"Awoo.TrayTap"

// The connection name handed to InitializeXamlDiagnosticsEx. One consumer per
// process, so the name only has to be stable, not unique to a run.
#define AWOO_TAP_ENDPOINT L"VisualDiagConnection1"

// Messages awoo posts to the window. Each returns an LRESULT; SendMessageW
// carries the scalar back across the process boundary with no marshalling.

// Returns AWOO_TAP_PROTOCOL_VERSION. awoo checks this first.
#define WM_AWOO_TAP_PROTOCOL (WM_APP + 1)

// Finds the stock volume icon in the tray's visual tree and collapses it.
// Returns 1 if the icon was found and hidden, 0 if nothing matched (the
// post-Windows-update failure mode) or the collapse did not take.
#define WM_AWOO_TAP_HIDE (WM_APP + 2)

// Restores the icon this DLL hid. Returns 1 always: restoring something not
// hidden is a no-op, not a failure.
#define WM_AWOO_TAP_RESTORE (WM_APP + 3)

// Returns 1 if the volume icon is currently hidden by this DLL, else 0.
#define WM_AWOO_TAP_QUERY (WM_APP + 4)
