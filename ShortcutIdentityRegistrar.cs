using System.Runtime.InteropServices;

namespace VlcShimDebugFr;

internal static class ShortcutIdentityRegistrar
{
    private const string ShortcutDirectoryName = "vlcshim";

    private static readonly PropertyKey AppUserModelIdKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        5);

    public static void EnsureShortcut(string appUserModelId, string displayName, string iconPath)
    {
        if (string.IsNullOrWhiteSpace(appUserModelId) || string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) || !Path.IsPathRooted(processPath))
        {
            return;
        }

        string shortcutsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            ShortcutDirectoryName);
        Directory.CreateDirectory(shortcutsRoot);

        string shortcutPath = Path.Combine(shortcutsRoot, SanitizeFileName(displayName) + ".lnk");

        var shellLink = (IShellLinkW)(object)new ShellLink();
        try
        {
            shellLink.SetPath(processPath);
            shellLink.SetArguments(string.Empty);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(processPath) ?? ".");
            shellLink.SetDescription(displayName);
            shellLink.SetIconLocation(File.Exists(iconPath) ? iconPath : processPath, 0);

            var propertyStore = (IPropertyStore)shellLink;
            var appIdVariant = PropVariant.FromString(appUserModelId);
            var appIdKey = AppUserModelIdKey;
            try
            {
                propertyStore.SetValue(ref appIdKey, ref appIdVariant);
                propertyStore.Commit();
            }
            finally
            {
                appIdVariant.Dispose();
            }

            ((IPersistFile)shellLink).Save(shortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath();
        void GetIDList();
        void SetIDList(IntPtr pidl);
        void GetDescription();
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory();
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments();
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey();
        void SetHotkey(short hotkey);
        void GetShowCmd();
        void SetShowCmd(int showCommand);
        void GetIconLocation();
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pathRelative, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string filePath);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct PropertyKey
    {
        public PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }

        public Guid FormatId { get; }

        public uint PropertyId { get; }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant : IDisposable
    {
        private const ushort VT_LPWSTR = 31;

        [FieldOffset(0)]
        private ushort _valueType;

        [FieldOffset(8)]
        private IntPtr _pointerValue;

        public static PropVariant FromString(string value)
        {
            return new PropVariant
            {
                _valueType = VT_LPWSTR,
                _pointerValue = Marshal.StringToCoTaskMemUni(value)
            };
        }

        public void Dispose()
        {
            PropVariantClear(ref this);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant propVariant);
}
