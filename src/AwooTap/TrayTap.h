// The TAP object awoo loads into Explorer to hide the stock volume icon.
//
// It is the COM object InitializeXamlDiagnosticsEx creates on awoo's behalf
// inside Explorer. Explorer's XAML runtime hands it the diagnostics site
// (IObjectWithSite::SetSite); from there it watches the taskbar's visual tree
// (IVisualTreeServiceCallback2), finds the system tray's volume icon, and can
// collapse or restore it. awoo talks to it through a message-only window —
// see AwooTapContract.h.
//
// Fresh MIT code. The XAML-diagnostics technique is Microsoft's own, and
// other projects (TranslucentTB, Windhawk) use it under the GPL; nothing here
// is taken from them.

#pragma once

#include <windows.h>
#include <xamlom.h>
#include <string>
#include <unordered_map>

// The CLSID awoo names when it injects this DLL. Mirrored in the C# host.
// {E6038BAE-6773-4567-BACF-9D407E974033}
extern "C" const CLSID CLSID_AwooTrayTap;

// Bookkeeping the class factory and DllCanUnloadNow share.
void AwooTapLockModule(bool lock);
LONG AwooTapModuleLocks();

/// <summary>
/// One live diagnostics session inside Explorer. Implements the TAP contract
/// (IObjectWithSite) and the visual-tree callback, and owns the message-only
/// window awoo drives it through.
/// </summary>
class TrayTap final : public IObjectWithSite, public IVisualTreeServiceCallback2
{
public:
    TrayTap();

    // IUnknown
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppv) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;

    // IObjectWithSite
    HRESULT STDMETHODCALLTYPE SetSite(IUnknown* site) override;
    HRESULT STDMETHODCALLTYPE GetSite(REFIID riid, void** ppv) override;

    // IVisualTreeServiceCallback / IVisualTreeServiceCallback2
    HRESULT STDMETHODCALLTYPE OnVisualTreeChange(
        ParentChildRelation relation, VisualElement element, VisualMutationType mutationType) override;
    HRESULT STDMETHODCALLTYPE OnElementStateChanged(
        InstanceHandle element, VisualElementState elementState, LPCWSTR context) override;

private:
    ~TrayTap();

    // What we remember about one element of the visual tree.
    struct Node
    {
        std::wstring type;
        std::wstring name;
        InstanceHandle parent;
    };

    // The message-only window awoo posts commands to.
    void EnsureWindow();
    void DestroyWindow();
    static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam);
    LRESULT HandleCommand(UINT msg);

    // The work the commands drive.
    bool FindVolumeIcon();                 // locates the IconView, sets m_target
    bool ApplyHide();                      // collapses m_target
    void ApplyRestore();                   // clears the collapse again
    bool ReadText(InstanceHandle handle, std::wstring& text) const;
    InstanceHandle NearestIconViewAncestor(InstanceHandle handle) const;

    static void Log(const wchar_t* format, ...);

    LONG m_ref;
    IVisualTreeService3* m_service;
    IXamlDiagnostics* m_diagnostics;
    HWND m_window;

    bool m_hideRequested;                  // awoo has asked us to hide
    bool m_applied;                        // and we have actually collapsed it
    InstanceHandle m_target;               // the volume IconView, or 0

    std::unordered_map<InstanceHandle, Node> m_nodes;
};
