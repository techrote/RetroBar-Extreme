using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace RetroBar.Utilities
{
    // UXUI-Extreme: items dragged out of shell sources (Start menu, Explorer, the desktop, Store
    // apps) as the shell itself describes them, rather than only as a list of file paths. Apps
    // without a file (Store apps, "shell:" items) have no path to point a shortcut at, so their
    // shortcut stores the shell item itself, which keeps the app's own icon and launch behaviour.
    public static class ShellDrop
    {
        public const string ShellIdListFormat = "Shell IDList Array";

        public class Dropped
        {
            public string Path;        // a real file or folder, or null
            public string Name;        // display name (no extension)
            public IntPtr IdList;      // absolute ID list for items without a path (free with CoTaskMemFree)
        }

        public static bool CanTake(System.Windows.IDataObject data) =>
            data != null && (data.GetDataPresent(System.Windows.DataFormats.FileDrop) || data.GetDataPresent(ShellIdListFormat));

        public static List<Dropped> Read(System.Windows.IDataObject data)
        {
            var result = new List<Dropped>();

            if (data is IDataObject com && data.GetDataPresent(ShellIdListFormat))
            {
                try
                {
                    Guid iid = typeof(IShellItemArray).GUID;
                    if (SHCreateShellItemArrayFromDataObject(com, ref iid, out IShellItemArray array) == 0 && array != null)
                    {
                        array.GetCount(out uint count);
                        for (uint i = 0; i < count; i++)
                        {
                            array.GetItemAt(i, out IShellItem item);
                            string path = DisplayName(item, SIGDN_FILESYSPATH);
                            string name = DisplayName(item, SIGDN_NORMALDISPLAY);
                            var dropped = new Dropped { Path = path, Name = name };
                            if (string.IsNullOrEmpty(path) || !(File.Exists(path) || Directory.Exists(path)))
                            {
                                dropped.Path = null;
                                if (SHGetIDListFromObject(item, out IntPtr pidl) == 0)
                                {
                                    dropped.IdList = pidl;
                                }
                            }
                            if (dropped.Path != null || dropped.IdList != IntPtr.Zero)
                            {
                                result.Add(dropped);
                            }
                        }
                    }
                }
                catch (Exception e) when (e is COMException || e is InvalidCastException || e is InvalidOperationException)
                {
                    result.Clear(); // fall back to the plain file list
                }
            }

            if (result.Count == 0 && data != null && data.GetDataPresent(System.Windows.DataFormats.FileDrop) &&
                data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)
            {
                result.AddRange(paths.Select(p => new Dropped { Path = p, Name = System.IO.Path.GetFileNameWithoutExtension(p) }));
            }

            return result;
        }

        // A shortcut to an item that has no file, made from its shell ID list.
        public static void SaveShortcut(IntPtr idList, string shortcutPath)
        {
            var link = (IShellLinkW)new ShellLink();
            link.SetIDList(idList);
            ((IPersistFile)link).Save(shortcutPath, true);
            Marshal.ReleaseComObject(link);
        }

        public static void Free(Dropped dropped)
        {
            if (dropped.IdList != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(dropped.IdList);
                dropped.IdList = IntPtr.Zero;
            }
        }

        public static string SafeFileName(string name)
        {
            string cleaned = string.Concat((name ?? "").Where(c => System.IO.Path.GetInvalidFileNameChars().All(x => x != c))).Trim();
            return string.IsNullOrEmpty(cleaned) ? "Shortcut" : cleaned;
        }

        private const uint SIGDN_NORMALDISPLAY = 0x00000000, SIGDN_FILESYSPATH = 0x80058000;

        private static string DisplayName(IShellItem item, uint kind)
        {
            try
            {
                item.GetDisplayName(kind, out IntPtr text);
                string value = Marshal.PtrToStringUni(text);
                Marshal.FreeCoTaskMem(text);
                return value;
            }
            catch (COMException)
            {
                return null; // e.g. no file system path for a Store app
            }
        }

        [DllImport("shell32.dll")]
        private static extern int SHCreateShellItemArrayFromDataObject(IDataObject pdo, ref Guid riid, out IShellItemArray ppv);

        [DllImport("shell32.dll")]
        private static extern int SHGetIDListFromObject([MarshalAs(UnmanagedType.IUnknown)] object punk, out IntPtr ppidl);

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemArray
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);
            void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
            void GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
            void GetAttributes(int attribFlags, uint sfgaoMask, out uint psfgaoAttribs);
            void GetCount(out uint pdwNumItems);
            void GetItemAt(uint dwIndex, out IShellItem ppsi);
            void EnumItems(out IntPtr ppenumShellItems);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }
}
