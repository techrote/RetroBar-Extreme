using ManagedShell.WindowsTray;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using TrayIcon = ManagedShell.WindowsTray.NotifyIcon;

namespace RetroBar.Controls
{
    // NotificationArea.UnpinnedIcons is a shared collection view on multi-monitor
    // configurations. The predicate must not capture any particular UI instance.
    public static class UnpinnedIconFilterLease
    {
        private sealed class Lease
        {
            internal int Owners;
            internal Predicate<object> Previous;
        }

        private static readonly Predicate<object> SharedFilter = item =>
            item is not TrayIcon icon ||
            (!icon.IsPinned && !icon.IsHidden && icon.GetBehavior() != NotifyIconBehavior.Remove);

        private static readonly Dictionary<ICollectionView, Lease> Active = new();

        public static void Acquire(ICollectionView view)
        {
            if (view == null) return;
            if (Active.TryGetValue(view, out Lease lease))
            {
                lease.Owners++;
                return;
            }

            Active.Add(view, new Lease { Owners = 1, Previous = view.Filter });
            view.Filter = SharedFilter;
        }

        public static void Release(ICollectionView view)
        {
            if (view == null || !Active.TryGetValue(view, out Lease lease)) return;
            if (--lease.Owners > 0) return;
            Active.Remove(view);
            if (ReferenceEquals(view.Filter, SharedFilter))
                view.Filter = lease.Previous;
        }

        public static int OwnerCount(ICollectionView view) =>
            view != null && Active.TryGetValue(view, out Lease lease) ? lease.Owners : 0;
    }
}
