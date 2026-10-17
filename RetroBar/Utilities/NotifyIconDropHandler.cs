using GongSolutions.Wpf.DragDrop;
using RetroBar.Controls;

namespace RetroBar.Utilities
{
    public class NotifyIconDropHandler : IDropTarget
    {
        private NotifyIconList _notifyIconList;

        public IDropInfo DropInFlight { get; set; }

        public NotifyIconDropHandler(NotifyIconList notifyIconList)
        {
            _notifyIconList = notifyIconList;
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if (_notifyIconList.ToggleTucked)
            {
                // The list holds the toggle as well, which the default handler can't place icons among.
                if (dropInfo.Data is ManagedShell.WindowsTray.NotifyIcon)
                {
                    dropInfo.Effects = System.Windows.DragDropEffects.Move;
                    dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                }
                return;
            }

            DragDrop.DefaultDropHandler.DragOver(dropInfo);
        }

#if !NETCOREAPP3_1_OR_GREATER
        public void DragEnter(IDropInfo dropInfo)
        {
            DragDrop.DefaultDropHandler.DragEnter(dropInfo);
        }

        public void DragLeave(IDropInfo dropInfo)
        {
            DragDrop.DefaultDropHandler.DragLeave(dropInfo);
        }
#endif

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            DropInFlight = dropInfo;
            if (!_notifyIconList.ToggleTucked)
            {
                DragDrop.DefaultDropHandler.Drop(dropInfo);
            }

            // Save post-drop state
            _notifyIconList.UpdateIconOrder(dropInfo);
            DropInFlight = null;
        }
    }
}
