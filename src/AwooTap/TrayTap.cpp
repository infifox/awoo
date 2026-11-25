#include "TrayTap.h"
#include "AwooTapContract.h"

#include <cstdio>
#include <cstdarg>
#include <cwchar>

// The module's own base address, which the linker provides; used as the
// HINSTANCE for the window class without a DllMain hook to capture it.
extern "C" IMAGE_DOS_HEADER __ImageBase;

// {E6038BAE-6773-4567-BACF-9D407E974033}
extern "C" const CLSID CLSID_AwooTrayTap =
    { 0xe6038bae, 0x6773, 0x4567, { 0xba, 0xcf, 0x9d, 0x40, 0x7e, 0x97, 0x40, 0x33 } };

namespace
{
    // Keeps the DLL loaded while any TAP object or class-factory lock is live.
    LONG g_moduleLocks = 0;

    // The glyphs the volume icon draws in its InnerTextBlock, in the Segoe
    // Fluent Icons font: mute, the four volume levels, muted-output, and the
    // "volume bars" variant. Any of these marks the tray icon we hide.
    bool IsVolumeGlyph(const std::wstring& text)
    {
        for (wchar_t ch : text)
        {
            switch (ch)
            {
            case 0xE74F: // Mute
            case 0xE992: // Volume0
            case 0xE993: // Volume1
            case 0xE994: // Volume2
            case 0xE995: // Volume3
            case 0xEA85: // VolumeDisabled
            case 0xEBC5: // VolumeBars
                return true;
            default:
                break;
            }
        }
        return false;
    }

    bool EndsWith(const std::wstring& value, const wchar_t* suffix)
    {
        size_t len = std::wcslen(suffix);
        return value.size() >= len && value.compare(value.size() - len, len, suffix) == 0;
    }
}

void AwooTapLockModule(bool lock)
{
    InterlockedExchangeAdd(&g_moduleLocks, lock ? 1 : -1);
}

LONG AwooTapModuleLocks()
{
    return g_moduleLocks;
}

TrayTap::TrayTap()
    : m_ref(1)
    , m_service(nullptr)
    , m_diagnostics(nullptr)
    , m_window(nullptr)
    , m_hideRequested(false)
    , m_applied(false)
    , m_target(0)
{
    AwooTapLockModule(true);
}

TrayTap::~TrayTap()
{
    DestroyWindow();
    if (m_diagnostics)
    {
        m_diagnostics->Release();
    }
    if (m_service)
    {
        m_service->Release();
    }
    AwooTapLockModule(false);
}

// --- IUnknown ---------------------------------------------------------------

HRESULT STDMETHODCALLTYPE TrayTap::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }

    if (riid == IID_IUnknown || riid == IID_IObjectWithSite)
    {
        *ppv = static_cast<IObjectWithSite*>(this);
    }
    else if (riid == __uuidof(IVisualTreeServiceCallback) ||
             riid == __uuidof(IVisualTreeServiceCallback2))
    {
        *ppv = static_cast<IVisualTreeServiceCallback2*>(this);
    }
    else
    {
        *ppv = nullptr;
        return E_NOINTERFACE;
    }

    AddRef();
    return S_OK;
}

ULONG STDMETHODCALLTYPE TrayTap::AddRef()
{
    return InterlockedIncrement(&m_ref);
}

ULONG STDMETHODCALLTYPE TrayTap::Release()
{
    LONG count = InterlockedDecrement(&m_ref);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

// --- IObjectWithSite --------------------------------------------------------

HRESULT STDMETHODCALLTYPE TrayTap::SetSite(IUnknown* site)
{
    // Drop any previous session first. SetSite(nullptr) is also how the
    // diagnostics host tears us down, so this is the whole cleanup path.
    if (m_service)
    {
        m_service->UnadviseVisualTreeChange(this);
        m_service->Release();
        m_service = nullptr;
    }
    if (m_diagnostics)
    {
        m_diagnostics->Release();
        m_diagnostics = nullptr;
    }

    if (!site)
    {
        // Leave the icon as awoo last asked for: the human chose that exiting
        // awoo restores it, and that restore is sent before teardown. If the
        // session drops for another reason we simply stop; Explorer brings
        // the icon back on its own next restart.
        DestroyWindow();
        m_nodes.clear();
        m_target = 0;
        m_applied = false;
        Log(L"SetSite(nullptr): session closed");
        return S_OK;
    }

    site->QueryInterface(IID_PPV_ARGS(&m_service));
    site->QueryInterface(IID_PPV_ARGS(&m_diagnostics));
    if (!m_service)
    {
        Log(L"SetSite: no IVisualTreeService3 on the site");
        return E_NOINTERFACE;
    }

    // Advise first: it replays the tree that already exists to us
    // synchronously, so m_nodes is populated before the window goes up. awoo
    // finds the window only after that, so the HIDE it then sends sees the
    // whole tree rather than racing the replay.
    HRESULT hr = m_service->AdviseVisualTreeChange(this);
    EnsureWindow();
    Log(L"SetSite: advised hr=0x%08X, nodes=%zu, window=%p", hr, m_nodes.size(), m_window);
    return S_OK;
}

HRESULT STDMETHODCALLTYPE TrayTap::GetSite(REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }
    if (m_service)
    {
        return m_service->QueryInterface(riid, ppv);
    }
    *ppv = nullptr;
    return E_FAIL;
}

// --- IVisualTreeServiceCallback2 -------------------------------------------

HRESULT STDMETHODCALLTYPE TrayTap::OnVisualTreeChange(
    ParentChildRelation relation, VisualElement element, VisualMutationType mutationType)
try
{
    if (mutationType == Add)
    {
        Node node;
        node.type = element.Type ? element.Type : L"";
        node.name = element.Name ? element.Name : L"";
        node.parent = relation.Parent;
        m_nodes[element.Handle] = std::move(node);

        // If awoo has asked to hide and we have not managed it yet, the
        // arrival of a tray icon's text block is the cue to try — this is
        // what re-hides after the taskbar is rebuilt and the tree repopulates.
        if (m_hideRequested && !m_applied &&
            m_nodes[element.Handle].name == L"InnerTextBlock")
        {
            if (FindVolumeIcon())
            {
                ApplyHide();
            }
        }
    }
    else // Remove
    {
        if (element.Handle == m_target)
        {
            m_target = 0;
            m_applied = false;
        }
        m_nodes.erase(element.Handle);
    }

    return S_OK;
}
catch (...)
{
    // Never let anything out into Explorer's XAML thread.
    return S_OK;
}

HRESULT STDMETHODCALLTYPE TrayTap::OnElementStateChanged(
    InstanceHandle, VisualElementState, LPCWSTR)
{
    return S_OK;
}

// --- The message window -----------------------------------------------------

void TrayTap::EnsureWindow()
{
    if (m_window)
    {
        return;
    }

    HINSTANCE instance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    WNDCLASSEXW wc = { sizeof(wc) };
    wc.lpfnWndProc = &TrayTap::WndProc;
    wc.hInstance = instance;
    wc.lpszClassName = AWOO_TAP_WINDOW_CLASS;
    // RegisterClassEx fails harmlessly if the class is already registered from
    // an earlier session; CreateWindowEx below still works.
    RegisterClassExW(&wc);

    m_window = CreateWindowExW(
        0, AWOO_TAP_WINDOW_CLASS, AWOO_TAP_WINDOW_TITLE,
        0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, instance, nullptr);

    if (m_window)
    {
        SetWindowLongPtrW(m_window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(this));
    }
    else
    {
        Log(L"EnsureWindow: CreateWindowEx failed, err=%lu", GetLastError());
    }
}

void TrayTap::DestroyWindow()
{
    if (m_window)
    {
        SetWindowLongPtrW(m_window, GWLP_USERDATA, 0);
        ::DestroyWindow(m_window);
        m_window = nullptr;
    }
}

LRESULT CALLBACK TrayTap::WndProc(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam)
{
    if (msg >= WM_AWOO_TAP_PROTOCOL && msg <= WM_AWOO_TAP_QUERY)
    {
        auto* self = reinterpret_cast<TrayTap*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
        if (self)
        {
            return self->HandleCommand(msg);
        }
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wparam, lparam);
}

LRESULT TrayTap::HandleCommand(UINT msg)
try
{
    switch (msg)
    {
    case WM_AWOO_TAP_PROTOCOL:
        return AWOO_TAP_PROTOCOL_VERSION;

    case WM_AWOO_TAP_HIDE:
    {
        m_hideRequested = true;
        if (!m_applied)
        {
            FindVolumeIcon();
            ApplyHide();
        }
        Log(L"HIDE: applied=%d target=%llu", m_applied ? 1 : 0,
            static_cast<unsigned long long>(m_target));
        return m_applied ? 1 : 0;
    }

    case WM_AWOO_TAP_RESTORE:
        m_hideRequested = false;
        ApplyRestore();
        Log(L"RESTORE done");
        return 1;

    case WM_AWOO_TAP_QUERY:
        return m_applied ? 1 : 0;

    default:
        return 0;
    }
}
catch (...)
{
    return 0;
}

// --- Finding and collapsing the icon ---------------------------------------

InstanceHandle TrayTap::NearestIconViewAncestor(InstanceHandle handle) const
{
    // Walk up the parent chain to the first System tray IconView. A bounded
    // loop: the tree is not deep, and the cap guards against a cycle the data
    // should never contain.
    for (int guard = 0; guard < 64 && handle != 0; ++guard)
    {
        auto it = m_nodes.find(handle);
        if (it == m_nodes.end())
        {
            return 0;
        }
        if (it->second.type == L"SystemTray.IconView")
        {
            return handle;
        }
        handle = it->second.parent;
    }
    return 0;
}

bool TrayTap::FindVolumeIcon()
{
    // The tray draws each status icon's glyph in a TextBlock named
    // InnerTextBlock; the one holding a volume glyph belongs to the volume
    // icon. We match the glyph rather than a name or automation id because the
    // glyph is what Windows itself keys the icon on, and it survives the
    // localisation and restructuring that a name does not.
    for (const auto& entry : m_nodes)
    {
        const Node& node = entry.second;
        bool looksLikeGlyph = node.name == L"InnerTextBlock" || EndsWith(node.type, L"TextBlock");
        if (!looksLikeGlyph)
        {
            continue;
        }

        InstanceHandle iconView = NearestIconViewAncestor(entry.first);
        if (iconView == 0)
        {
            continue;
        }

        std::wstring text;
        if (ReadProperty(entry.first, L"Text", &text, nullptr) && IsVolumeGlyph(text))
        {
            m_target = iconView;
            return true;
        }
    }

    return false;
}

/// <summary>
/// Reads one property of an element out of its property chain: its value as
/// text, and the index <c>SetProperty</c> and <c>ClearProperty</c> take.
/// Either output may be null.
/// </summary>
/// <remarks>
/// The chain is the only dependable source of that index. GetPropertyIndex,
/// which this used to ask, is an Edit-and-Continue helper that resolves a name
/// against the element's own type — and it answers nothing for Visibility on
/// the tray's icons, which SystemTray.IconView inherits from UIElement rather
/// than declaring itself. That is what made every hide fail. The chain reports
/// every property an element has, inherited ones included, each with its index.
/// </remarks>
bool TrayTap::ReadProperty(
    InstanceHandle handle, const wchar_t* name, std::wstring* value, unsigned int* index) const
{
    if (!m_service)
    {
        return false;
    }

    UINT sourceCount = 0;
    UINT valueCount = 0;
    PropertyChainSource* sources = nullptr;
    PropertyChainValue* values = nullptr;

    HRESULT hr = m_service->GetPropertyValuesChain(
        handle, &sourceCount, &sources, &valueCount, &values);
    if (FAILED(hr))
    {
        Log(L"ReadProperty(%llu, %s): the chain came back 0x%08X",
            static_cast<unsigned long long>(handle), name, hr);
        return false;
    }

    bool found = false;
    for (UINT i = 0; i < valueCount; ++i)
    {
        if (values[i].PropertyName && std::wcscmp(values[i].PropertyName, name) == 0)
        {
            if (value)
            {
                *value = values[i].Value ? values[i].Value : L"";
            }
            if (index)
            {
                *index = values[i].Index;
            }
            found = true;
            break;
        }
    }

    // The arrays and their strings are ours to free (CoTaskMemAlloc'd arrays of
    // SysAllocString'd members). Leaking them would leak inside Explorer.
    for (UINT i = 0; i < valueCount; ++i)
    {
        SysFreeString(values[i].Type);
        SysFreeString(values[i].DeclaringType);
        SysFreeString(values[i].ValueType);
        SysFreeString(values[i].ItemType);
        SysFreeString(values[i].Value);
        SysFreeString(values[i].PropertyName);
    }
    for (UINT i = 0; i < sourceCount; ++i)
    {
        SysFreeString(sources[i].TargetType);
        SysFreeString(sources[i].Name);
        SysFreeString(sources[i].SrcInfo.FileName);
        SysFreeString(sources[i].SrcInfo.Hash);
    }
    CoTaskMemFree(values);
    CoTaskMemFree(sources);

    return found;
}

/// <summary>
/// The index of an element's Visibility: out of its property chain, or failing
/// that out of the Edit-and-Continue lookup, which is kept only for the day a
/// build stops reporting the property in the chain.
/// </summary>
bool TrayTap::ResolveVisibility(InstanceHandle handle, unsigned int& index) const
{
    if (ReadProperty(handle, L"Visibility", nullptr, &index))
    {
        return true;
    }

    HRESULT hr = m_service->GetPropertyIndex(handle, L"Visibility", &index);
    if (SUCCEEDED(hr))
    {
        return true;
    }

    Log(L"ResolveVisibility: not in the chain, and GetPropertyIndex says 0x%08X", hr);
    return false;
}

/// <summary>
/// A <c>Visibility.Collapsed</c> to assign, built by the service. Zero where
/// it could not be made.
/// </summary>
/// <remarks>
/// Visibility is an enum, so the value cannot be handed over as a string: it
/// is constructed from the metadata name of its type and one of its value
/// names. That type name has differed between XAML generations, so rather than
/// bet on a spelling this asks the service which enums it knows and uses the
/// name it gives back. The spellings awoo used to guess at stay on as a
/// fallback for a build whose GetEnums says nothing.
/// </remarks>
InstanceHandle TrayTap::MakeCollapsed()
{
    if (m_visibilityEnum.empty())
    {
        UINT count = 0;
        EnumType* enums = nullptr;
        HRESULT hr = m_service->GetEnums(&count, &enums);
        if (SUCCEEDED(hr) && enums)
        {
            for (UINT i = 0; i < count; ++i)
            {
                std::wstring name = enums[i].Name ? enums[i].Name : L"";
                if (m_visibilityEnum.empty() &&
                    (name == L"Visibility" || EndsWith(name, L".Visibility")))
                {
                    m_visibilityEnum = name;
                }

                SysFreeString(enums[i].Name);
                if (enums[i].ValueInts)
                {
                    SafeArrayDestroy(enums[i].ValueInts);
                }
                if (enums[i].ValueStrings)
                {
                    SafeArrayDestroy(enums[i].ValueStrings);
                }
            }
            CoTaskMemFree(enums);
        }
        Log(L"MakeCollapsed: GetEnums hr=0x%08X of %u gave [%s]", hr, count, m_visibilityEnum.c_str());
    }

    std::wstring names[3];
    int attempts = 0;
    if (!m_visibilityEnum.empty())
    {
        names[attempts++] = m_visibilityEnum;
    }
    names[attempts++] = L"Windows.UI.Xaml.Visibility";
    names[attempts++] = L"Visibility";

    for (int i = 0; i < attempts; ++i)
    {
        BSTR type = SysAllocString(names[i].c_str());
        BSTR value = SysAllocString(L"Collapsed");
        InstanceHandle collapsed = 0;
        HRESULT hr = m_service->CreateInstance(type, value, &collapsed);
        SysFreeString(type);
        SysFreeString(value);

        if (SUCCEEDED(hr) && collapsed != 0)
        {
            return collapsed;
        }
        Log(L"MakeCollapsed: CreateInstance([%s], Collapsed) hr=0x%08X", names[i].c_str(), hr);
    }

    return 0;
}

bool TrayTap::ApplyHide()
{
    if (m_applied)
    {
        return true;
    }
    if (m_target == 0 || !m_service)
    {
        return false;
    }

    unsigned int visibilityIndex = 0;
    if (!ResolveVisibility(m_target, visibilityIndex))
    {
        return false;
    }

    InstanceHandle collapsed = MakeCollapsed();
    if (collapsed == 0)
    {
        Log(L"ApplyHide: could not make a Collapsed value");
        return false;
    }

    HRESULT hr = m_service->SetProperty(m_target, collapsed, visibilityIndex);
    if (FAILED(hr))
    {
        Log(L"ApplyHide: SetProperty(index=%u) failed hr=0x%08X", visibilityIndex, hr);
        return false;
    }

    // Read it back rather than trust the call: what this returns is what the
    // settings window's note is written from, so it should say what the tray
    // looks like rather than what awoo asked for. The chain renders an enum as
    // its number rather than its name, so Collapsed comes back as "1" — accept
    // either, and let anything else stand as a hide that did not take.
    std::wstring visibility;
    if (ReadProperty(m_target, L"Visibility", &visibility, nullptr) &&
        visibility != L"Collapsed" && visibility != L"1")
    {
        Log(L"ApplyHide: set it, but Visibility reads back as [%s]", visibility.c_str());
        return false;
    }

    m_applied = true;
    return true;
}

void TrayTap::ApplyRestore()
{
    if (m_target == 0 || !m_service)
    {
        m_applied = false;
        return;
    }

    // Clearing the local value returns Visibility to its default, which is
    // Visible — no need to construct a value to put it back.
    unsigned int visibilityIndex = 0;
    if (ResolveVisibility(m_target, visibilityIndex))
    {
        HRESULT hr = m_service->ClearProperty(m_target, visibilityIndex);
        if (FAILED(hr))
        {
            Log(L"ApplyRestore: ClearProperty(index=%u) failed hr=0x%08X", visibilityIndex, hr);
        }
    }
    m_applied = false;
}

// --- Logging ----------------------------------------------------------------

void TrayTap::Log(const wchar_t* format, ...)
{
    wchar_t line[1024];
    va_list args;
    va_start(args, format);
    _vsnwprintf_s(line, _countof(line), _TRUNCATE, format, args);
    va_end(args);

    OutputDebugStringW(L"[AwooTap] ");
    OutputDebugStringW(line);
    OutputDebugStringW(L"\n");

    // A best-effort trace alongside it, since the injected DLL is otherwise
    // unobservable: this file is the only account of what it saw inside
    // Explorer. It goes beside the DLL first, so a build's log lands in the
    // output folder that produced it and whoever is working on the feature can
    // read it; an installed copy cannot write next to itself, and falls back to
    // Explorer's %TEMP%.
    //
    // Room for the name is checked by hand throughout: the secure CRT's string
    // calls answer an overflow by invoking the invalid-parameter handler, which
    // inside Explorer would take Explorer down with it.
    static const wchar_t kName[] = L"awoo-tap.log";
    static const size_t kNameLength = _countof(kName) - 1;

    wchar_t path[MAX_PATH];
    HANDLE file = INVALID_HANDLE_VALUE;

    DWORD n = GetModuleFileNameW(reinterpret_cast<HMODULE>(&__ImageBase), path, MAX_PATH);
    if (n != 0 && n < MAX_PATH)
    {
        wchar_t* slash = wcsrchr(path, L'\\');
        if (slash && (slash + 1 - path) + kNameLength < MAX_PATH)
        {
            wcscpy_s(slash + 1, MAX_PATH - (slash + 1 - path), kName);
            file = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
                nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
        }
    }

    if (file == INVALID_HANDLE_VALUE)
    {
        n = GetEnvironmentVariableW(L"TEMP", path, MAX_PATH);
        if (n == 0 || n + 1 + kNameLength >= MAX_PATH)
        {
            return;
        }
        path[n] = L'\\';
        wcscpy_s(path + n + 1, MAX_PATH - n - 1, kName);
        file = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    }

    if (file == INVALID_HANDLE_VALUE)
    {
        return;
    }

    char utf8[2048];
    int bytes = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, sizeof(utf8) - 2, nullptr, nullptr);
    if (bytes > 1)
    {
        utf8[bytes - 1] = '\r';
        utf8[bytes] = '\n';
        DWORD written = 0;
        WriteFile(file, utf8, static_cast<DWORD>(bytes + 1), &written, nullptr);
    }
    CloseHandle(file);
}
