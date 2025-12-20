using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace InstallerBootstrapper;

[SupportedOSPlatform("windows")]
public sealed class FolderPicker
{
    public string? PickFolder(string? initialDirectory)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        const uint fosPickFolders = 0x00000020;
        var hr = dialog.SetOptions(fosPickFolders | GetExistingOptions(dialog));
        Marshal.ThrowExceptionForHR(hr);

        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            var directoryShellItem = CreateItemFromParsingName(initialDirectory);
            if (directoryShellItem is not null)
            {
                dialog.SetFolder(directoryShellItem);
            }
        }

        hr = dialog.Show(IntPtr.Zero);
        if (hr == HRESULT.ERROR_CANCELLED)
        {
            return null;
        }

        Marshal.ThrowExceptionForHR(hr);
        dialog.GetResult(out var item);
        item.GetDisplayName(SIGDN.FILESYSPATH, out var pathPtr);
        var path = Marshal.PtrToStringUni(pathPtr);
        Marshal.FreeCoTaskMem(pathPtr);

        return path;
    }

    private static IShellItem? CreateItemFromParsingName(string path)
    {
        var iidShellItem = typeof(IShellItem).GUID;
        var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iidShellItem, out var item);
        return hr == HRESULT.S_OK ? item : null;
    }

    private static uint GetExistingOptions(IFileOpenDialog dialog)
    {
        dialog.GetOptions(out var options);
        return options;
    }

    private static class HRESULT
    {
        public const int S_OK = 0;
        public const int ERROR_CANCELLED = unchecked((int)0x800704C7);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, ref Guid riid, out IShellItem ppv);
}

[ComImport]
[Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
[ClassInterface(ClassInterfaceType.None)]
internal class FileOpenDialog
{
}

[ComImport]
[Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOpenDialog
{
    [PreserveSig] int Show(IntPtr parent);
    void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
    void SetFileTypeIndex(uint iFileType);
    void GetFileTypeIndex(out uint piFileType);
    void Advise(IntPtr pfde, out uint pdwCookie);
    void Unadvise(uint dwCookie);
    [PreserveSig] int SetOptions(uint fos);
    [PreserveSig] int GetOptions(out uint pfos);
    void SetDefaultFolder(IShellItem psi);
    void SetFolder(IShellItem psi);
    void GetFolder(out IShellItem ppsi);
    void GetCurrentSelection(out IShellItem ppsi);
    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetFileName(out IntPtr pszName);
    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
    void GetResult(out IShellItem ppsi);
    void AddPlace(IShellItem psi, uint fdap);
    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
    void Close(int hr);
    void SetClientGuid(ref Guid guid);
    void ClearClientData();
    void SetFilter([MarshalAs(UnmanagedType.IUnknown)] object pFilter);
    void GetResults(out IShellItemArray ppenum);
    void GetSelectedItems(out IShellItemArray ppsai);
}

internal enum SIGDN : uint
{
    FILESYSPATH = 0x80058000,
}

[ComImport]
[Guid("b4cbaccc-7c86-4807-9e45-46a86aa63c07")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    void BindToHandler();
    void GetPropertyStore();
    void GetPropertyDescriptionList();
    void GetAttributes();
    void GetCount(out uint pdwNumItems);
    void GetItemAt(uint dwIndex, out IShellItem ppsi);
    void EnumItems(out IntPtr ppenumShellItems);
}

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler();
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
    void GetAttributes();
    void Compare();
}
