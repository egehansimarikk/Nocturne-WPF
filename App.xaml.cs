using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace Nocturne
{
    public partial class App : Application
    {
        private const string AppId = "com.egehansimarikk.nocturne";

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        protected override void OnStartup(StartupEventArgs e)
        {
            IconHelper.EnsureTrimmedIcoFile();
            SetCurrentProcessExplicitAppUserModelID(AppId);
            RegisterAppUserModelId();
            EnsureStartMenuShortcut();
            base.OnStartup(e);
        }

        private static string GetResolvedIconPath()
        {
            return File.Exists(IconHelper.TrimmedIcoPath)
                ? IconHelper.TrimmedIcoPath
                : Path.Combine(AppContext.BaseDirectory, "Assets", "nocturne.ico");
        }

        private static void RegisterAppUserModelId()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{AppId}");
                if (key != null)
                {
                    key.SetValue("DisplayName", "Nocturne", RegistryValueKind.String);
                    key.SetValue("IconBackgroundColor", "0", RegistryValueKind.String);

                    string icoPath = GetResolvedIconPath();
                    if (File.Exists(icoPath))
                        key.SetValue("IconUri", icoPath, RegistryValueKind.String);
                }
            }
            catch { }
        }

        private static void EnsureStartMenuShortcut()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                    return;

                string programsDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string shortcutPath = Path.Combine(programsDir, "Nocturne.lnk");
                string icoPath = GetResolvedIconPath();

                var shellLink = (IShellLinkW)new ShellLink();
                shellLink.SetPath(exePath);
                shellLink.SetWorkingDirectory(AppContext.BaseDirectory);
                shellLink.SetDescription("Nocturne");

                if (File.Exists(icoPath))
                    shellLink.SetIconLocation(icoPath, 0);

                var propertyStore = (IPropertyStore)shellLink;
                var appIdKey = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
                var pv = PropVariant.FromString(AppId);
                try
                {
                    propertyStore.SetValue(ref appIdKey, ref pv);
                    propertyStore.Commit();
                }
                finally
                {
                    pv.Clear();
                }

                var persistFile = (IPersistFile)shellLink;
                persistFile.Save(shortcutPath, true);
            }
            catch { }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public uint pid;
            public PropertyKey(Guid fmtid, uint pid)
            {
                this.fmtid = fmtid;
                this.pid = pid;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr ptr;

            public static PropVariant FromString(string value) => new()
            {
                vt = 31, // VT_LPWSTR
                ptr = Marshal.StringToCoTaskMemUni(value)
            };

            public void Clear()
            {
                if (vt == 31 && ptr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptr);
                    ptr = IntPtr.Zero;
                    vt = 0;
                }
            }
        }
    }
}