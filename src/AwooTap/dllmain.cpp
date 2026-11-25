// The DLL's COM entry points. InitializeXamlDiagnosticsEx loads this DLL into
// Explorer and creates our TAP object from it by CLSID, so the DLL has to be
// a self-registering in-proc COM server in the ordinary way: a class object
// for CLSID_AwooTrayTap, and DllCanUnloadNow to say when it is safe to go.

#include "TrayTap.h"

#include <new>

namespace
{
    // The class object the loader asks for before it creates the TAP. One
    // stateless factory serves every request; it only ever makes a TrayTap.
    class ClassFactory final : public IClassFactory
    {
    public:
        ClassFactory() : m_ref(1) { AwooTapLockModule(true); }

        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppv) override
        {
            if (!ppv)
            {
                return E_POINTER;
            }
            if (riid == IID_IUnknown || riid == IID_IClassFactory)
            {
                *ppv = static_cast<IClassFactory*>(this);
                AddRef();
                return S_OK;
            }
            *ppv = nullptr;
            return E_NOINTERFACE;
        }

        ULONG STDMETHODCALLTYPE AddRef() override { return InterlockedIncrement(&m_ref); }

        ULONG STDMETHODCALLTYPE Release() override
        {
            LONG count = InterlockedDecrement(&m_ref);
            if (count == 0)
            {
                delete this;
            }
            return count;
        }

        HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
        {
            if (!ppv)
            {
                return E_POINTER;
            }
            *ppv = nullptr;
            if (outer)
            {
                return CLASS_E_NOAGGREGATION;
            }

            auto* tap = new (std::nothrow) TrayTap();
            if (!tap)
            {
                return E_OUTOFMEMORY;
            }

            HRESULT hr = tap->QueryInterface(riid, ppv);
            tap->Release();
            return hr;
        }

        HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override
        {
            AwooTapLockModule(lock != FALSE);
            return S_OK;
        }

    private:
        ~ClassFactory() { AwooTapLockModule(false); }
        LONG m_ref;
    };
}

extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

_Check_return_ STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _Outptr_ void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }
    *ppv = nullptr;

    if (rclsid != CLSID_AwooTrayTap)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    auto* factory = new (std::nothrow) ClassFactory();
    if (!factory)
    {
        return E_OUTOFMEMORY;
    }

    HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

__control_entrypoint(DllExport) STDAPI DllCanUnloadNow()
{
    return AwooTapModuleLocks() == 0 ? S_OK : S_FALSE;
}
