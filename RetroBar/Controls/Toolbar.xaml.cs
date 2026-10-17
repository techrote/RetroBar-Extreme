using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ManagedShell.Common.Helpers;
using ManagedShell.Common.Logging;
using ManagedShell.ShellFolders;
using ManagedShell.ShellFolders.Enums;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    /// <summary>
    /// Interaction logic for Toolbar.xaml
    /// </summary>
    public partial class Toolbar : UserControl
    {
        private bool _ignoreNextUpdate;
        private bool _isLoaded;

        private enum MenuItem : uint
        {
            OpenParentFolder = CommonContextMenuItem.Paste + 1,
            ChangeIcon, // UXUI-Extreme (UX-015)
            ResetIcon
        }

        public static DependencyProperty PathProperty = DependencyProperty.Register(nameof(Path), typeof(string), typeof(Toolbar), new PropertyMetadata(OnPathChanged));

        public string Path
        {
            get => (string)GetValue(PathProperty);
            set
            {
                SetValue(PathProperty, value);
                SetupFolder(value);
            }
        }

        private static DependencyProperty FolderProperty = DependencyProperty.Register(nameof(Folder), typeof(ShellFolder), typeof(Toolbar));

        public static DependencyProperty HostProperty = DependencyProperty.Register(nameof(Host), typeof(Taskbar), typeof(Toolbar), new PropertyMetadata(HostChangedCallback));

        public Taskbar Host
        {
            get { return (Taskbar)GetValue(HostProperty); }
            set { SetValue(HostProperty, value); }
        }

        public ToolbarDropHandler DropHandler { get; set; }

        private ShellFolder Folder
        {
            get => (ShellFolder)GetValue(FolderProperty);
            set
            {
                SetValue(FolderProperty, value);
                SetItemsSource();
            }
        }

        public Toolbar()
        {
            DropHandler = new ToolbarDropHandler(this);

            InitializeComponent();

            // UXUI-Extreme: file drops are taken here, ahead of the drag-drop library's handlers on
            // the items control (they left the drop unaccepted, so programs never became shortcuts).
            AllowDrop = true;
            AddHandler(PreviewDragEnterEvent, new DragEventHandler(FileDrag_Over));
            AddHandler(PreviewDragOverEvent, new DragEventHandler(FileDrag_Over));
            AddHandler(PreviewDropEvent, new DragEventHandler(FileDrag_Drop));
        }

        // The effect to offer: a link, or a copy if the source does not offer links. Never Move: that would make the source delete the original. A source that
        // offers only Copy or Move (many do) used to be refused because Link was insisted on.
        private static DragDropEffects EffectFor(DragEventArgs e)
        {
            if (e.AllowedEffects.HasFlag(DragDropEffects.Link)) return DragDropEffects.Link;
            if (e.AllowedEffects.HasFlag(DragDropEffects.Copy)) return DragDropEffects.Copy;
            return DragDropEffects.None;
        }

        private void FileDrag_Over(object sender, DragEventArgs e)
        {
            if (ShellDrop.CanTake(e.Data))
            {
                e.Effects = EffectFor(e);
                e.Handled = true;
            }
        }

        private void FileDrag_Drop(object sender, DragEventArgs e)
        {
            if (!ShellDrop.CanTake(e.Data))
            {
                return;
            }

            var dropped = ShellDrop.Read(e.Data);
            e.Effects = EffectFor(e);
            e.Handled = true;

            // After the drop has returned to the source (a slow shortcut write must not hold it up).
            Dispatcher.BeginInvoke(new Action(() => AddDropped(dropped)));
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.QuickLaunchOrder))
            {
                if (_ignoreNextUpdate)
                {
                    _ignoreNextUpdate = false;
                    return;
                }

                Refresh();
            }
            else if (e.PropertyName == nameof(Settings.TaskbarScale))
            {
                Refresh();
            }
            else if (e.PropertyName == nameof(Settings.QuickLaunchAlignH) || e.PropertyName == nameof(Settings.QuickLaunchAlignV))
            {
                ApplyQuickAlignment();
            }
            else if (e.PropertyName == nameof(Settings.QuickLaunchButtonFill))
            {
                // The icons are boxed (or not) a moment later; measure the cell after that.
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(FitIconColumns));
            }
        }

        private void Refresh()
        {
            if (Folder == null)
            {
                return;
            }

            ListCollectionView cvs = (ListCollectionView)CollectionViewSource.GetDefaultView(Folder.Files);
            cvs.Refresh();
        }

        private void SetupFolder(string path)
        {
            Folder?.Dispose();
            Folder = new ShellFolder(Environment.ExpandEnvironmentVariables(path), IntPtr.Zero, true);
        }

        private void UnloadFolder()
        {
            Folder?.Dispose();
            Folder = null;
        }

        private void SetItemsSource()
        {
            if (Folder != null)
            {
                ToolbarItems.ItemsSource = Folder.Files;
                ListCollectionView cvs = (ListCollectionView)CollectionViewSource.GetDefaultView(Folder.Files);
                cvs.CustomSort = new ToolbarSorter(this);
            }
        }

        // The Show desktop button inside the quick launch area, at its leading edge.
        public void SetDesktopCell(bool visible, ManagedShell.WindowsTasks.TasksService tasks)
        {
            if (visible && DesktopCell.TasksService == null && tasks != null)
            {
                DesktopCell.TasksService = tasks;
            }

            DockPanel.SetDock(DesktopCell, Host != null && Host.Orientation == System.Windows.Controls.Orientation.Vertical ? Dock.Top : Dock.Left);
            DesktopCell.Visibility = visible && DesktopCell.TasksService != null ? Visibility.Visible : Visibility.Collapsed;
        }

        // Where the icons sit within the quick launch area (it can be longer than its icons).
        private void ApplyQuickAlignment()
        {
            Settings s = Settings.Instance;
            switch (s.QuickLaunchAlignH)
            {
                case TaskButtonAlignOption.Centre: ToolbarItems.HorizontalAlignment = HorizontalAlignment.Center; break;
                case TaskButtonAlignOption.Right: ToolbarItems.HorizontalAlignment = HorizontalAlignment.Right; break;
                default: ToolbarItems.ClearValue(HorizontalAlignmentProperty); break;
            }

            switch (s.QuickLaunchAlignV)
            {
                case VerticalPlaceOption.Centre: ToolbarItems.VerticalAlignment = VerticalAlignment.Center; break;
                case VerticalPlaceOption.Bottom: ToolbarItems.VerticalAlignment = VerticalAlignment.Bottom; break;
                default: ToolbarItems.ClearValue(VerticalAlignmentProperty); break;
            }
        }

        public void SaveItemOrder()
        {
            List<string> itemPaths = new List<string>();

            foreach (ShellFile file in ((ListCollectionView)CollectionViewSource.GetDefaultView(Folder.Files)).OfType<ShellFile>())
            {
                itemPaths.Add(file.Path);
            }

            // small optimization, only other toolbars with this folder need to reload when the setting is saved.
            _ignoreNextUpdate = true;

            Settings.Instance.QuickLaunchOrder = itemPaths;
        }

        public void AddToSource(StringCollection filesToAdd)
        {
            AddDropped(filesToAdd.Cast<string>().Select(p => new ShellDrop.Dropped { Path = p, Name = System.IO.Path.GetFileNameWithoutExtension(p) }).ToList());
        }

        // Makes a shortcut in the quick launch folder for each dropped item.
        public void AddDropped(List<ShellDrop.Dropped> items)
        {
            string sourcePath = Environment.ExpandEnvironmentVariables(Path);

            foreach (ShellDrop.Dropped item in items)
            {
                try
                {
                    string destinationFileName = ShellDrop.SafeFileName(item.Path != null ? System.IO.Path.GetFileNameWithoutExtension(item.Path) : item.Name);
                    if (item.Path != null && string.IsNullOrEmpty(System.IO.Path.GetFileNameWithoutExtension(item.Path)))
                    {
                        destinationFileName = ShellDrop.SafeFileName(item.Name); // a drive or folder root
                    }
                    string destinationPath = System.IO.Path.Combine(sourcePath, destinationFileName + ".lnk");
                    int dupCount = 0;

                    while (ShellHelper.Exists(destinationPath))
                    {
                        dupCount++;
                        destinationPath = System.IO.Path.Combine(sourcePath, $"{destinationFileName} ({dupCount}).lnk");
                    }

                    if (item.Path == null)
                    {
                        ShellDrop.SaveShortcut(item.IdList, destinationPath);
                    }
                    else if (string.Equals(System.IO.Path.GetExtension(item.Path), ".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        System.IO.File.Copy(item.Path, destinationPath); // already a shortcut: keep it as is
                    }
                    else
                    {
                        ShellLinkHelper.CreateAndSave(item.Path, destinationPath);
                    }
                }
                catch (Exception e)
                {
                    ShellLogger.Error($"Toolbar: Unable to save shortcut to {item.Path ?? item.Name}", e);
                }
                finally
                {
                    ShellDrop.Free(item);
                }
            }
        }

        #region Events
        private static void OnPathChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Toolbar toolbar)
            {
                toolbar.SetupFolder((string)e.NewValue);
            }
        }

        private void ToolbarIcon_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ToolbarButton icon = sender as ToolbarButton;
            if (icon == null)
            {
                return;
            }

            Mouse.Capture(null);
            ShellFile file = icon.DataContext as ShellFile;

            if (file == null || string.IsNullOrWhiteSpace(file.Path))
            {
                return;
            }

            if (InvokeContextMenu(file, false))
            {
                e.Handled = true;
            }
        }

        private void ToolbarIcon_OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ToolbarButton icon = sender as ToolbarButton;
            if (icon == null)
            {
                return;
            }
            
            ShellFile file = icon.DataContext as ShellFile;

            if (InvokeContextMenu(file, true))
            {
                e.Handled = true;
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visible)
            {
                if (visible)
                {
                    if (Folder != null)
                    {
                        return;
                    }

                    SetupFolder(Path);
                }
                else
                {
                    UnloadFolder();
                }
            }
        }

        private void Toolbar_TaskbarHotkeyPressed(object sender, HotkeyManager.TaskbarHotkeyEventArgs e)
        {
            if (Settings.Instance.WinNumHotkeysAction == WinNumHotkeysOption.InvokeQuickLaunch && Host.Screen.Primary)
            {
                try
                {
                    ListCollectionView items = (ListCollectionView)CollectionViewSource.GetDefaultView(Folder.Files);

                    bool exists = items.MoveCurrentToPosition(e.index);
                    
                    if (exists) InvokeContextMenu((ShellFile)items.CurrentItem, false);

                }
                catch (ArgumentOutOfRangeException) { }
            }
        }
        #endregion

        #region Context menu
        private ShellMenuCommandBuilder GetFileCommandBuilder(ShellFile file)
        {
            if (file == null)
            {
                return new ShellMenuCommandBuilder();
            }

            ShellMenuCommandBuilder builder = new ShellMenuCommandBuilder();

            builder.AddSeparator();
            builder.AddCommand(new ShellMenuCommand
            {
                Flags = MFT.BYCOMMAND,
                Label = (string)FindResource("open_folder"),
                UID = (uint)MenuItem.OpenParentFolder
            });

            // UXUI-Extreme (UX-015): custom PNG icon for this item
            builder.AddCommand(new ShellMenuCommand
            {
                Flags = MFT.BYCOMMAND,
                Label = (string)FindResource("uxui_ql_change_icon"),
                UID = (uint)MenuItem.ChangeIcon
            });
            if (!string.IsNullOrEmpty(CustomIcons.QuickLaunchIcon(file.Path)))
            {
                builder.AddCommand(new ShellMenuCommand
                {
                    Flags = MFT.BYCOMMAND,
                    Label = (string)FindResource("uxui_ql_reset_icon"),
                    UID = (uint)MenuItem.ResetIcon
                });
            }

            return builder;
        }

        private bool InvokeContextMenu(ShellFile file, bool isInteractive)
        {
            if (file == null)
            {
                return false;
            }
            
            var _ = new ShellItemContextMenu(new ShellItem[] { file }, Folder, IntPtr.Zero, HandleFileAction, isInteractive, false, new ShellMenuCommandBuilder(), GetFileCommandBuilder(file));
            return true;
        }

        private bool HandleFileAction(string action, ShellItem[] items, bool allFolders)
        {
            if (action == ((uint)MenuItem.OpenParentFolder).ToString())
            {
                ShellHelper.StartProcess(Folder.Path);
                return true;
            }

            // UXUI-Extreme (UX-015)
            if (action == ((uint)MenuItem.ChangeIcon).ToString() && items.Length > 0)
            {
                ChooseIcon(items[0].Path);
                return true;
            }
            if (action == ((uint)MenuItem.ResetIcon).ToString() && items.Length > 0)
            {
                CustomIcons.SetQuickLaunchIcon(items[0].Path, null);
                return true;
            }

            return false;
        }
        #endregion

        private void Initialize()
        {
            if (!_isLoaded && Host != null)
            {
                Settings.Instance.PropertyChanged += Settings_PropertyChanged;
                Host.hotkeyManager.TaskbarHotkeyPressed += Toolbar_TaskbarHotkeyPressed;

                _isLoaded = true;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Initialize();
            HookIconFit(); // UXUI-Extreme
            ApplyQuickAlignment();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
            if (Host != null)
            {
                Host.hotkeyManager.TaskbarHotkeyPressed -= Toolbar_TaskbarHotkeyPressed;
            }

            _isLoaded = false;
        }

        private static void HostChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Toolbar toolbar && e.OldValue == null && e.NewValue != null)
            {
                toolbar.Initialize();
            }
        }
    }
}