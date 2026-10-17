# Developer harness: starts a real OLE file drag (a FileDrop of an exe) from a small window and
# drops it at the given screen point, to test drops onto RetroBar's quick launch without a human.
# Usage: powershell -STA -File drag-test.ps1 -X 52 -Y 1830 [-File C:\Windows\System32\calc.exe]
# Writes the drop effect (None / Copy / Link ...) to %TEMP%\drag-result.txt.
param([int]$X = 52, [int]$Y = 1830, [string]$File = "$env:SystemRoot\System32\calc.exe", [string]$Allowed = 'Copy,Move,Link')

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Threading;
public static class Mover {
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
    // Presses on (sx,sy), drags to (tx,ty) and releases, on its own thread (the drag blocks the UI thread).
    public static void Start(int sx, int sy, int tx, int ty) {
        var t = new Thread(() => {
            Thread.Sleep(400);
            SetCursorPos(sx, sy); Thread.Sleep(300);
            mouse_event(2, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(300);
            for (int i = 1; i <= 30; i++) { SetCursorPos(sx + (tx - sx) * i / 30, sy + (ty - sy) * i / 30); Thread.Sleep(30); }
            Thread.Sleep(500);
            mouse_event(4, 0, 0, 0, UIntPtr.Zero);
        });
        t.IsBackground = true; t.Start();
    }
}
"@

$form = New-Object System.Windows.Forms.Form
$form.Text = 'drag source'; $form.StartPosition = 'Manual'; $form.Location = '300,300'; $form.Size = '220,120'
$form.TopMost = $true
$form.Add_MouseDown({
    $data = New-Object System.Windows.Forms.DataObject
    $files = New-Object System.Collections.Specialized.StringCollection
    $files.Add($File) | Out-Null
    $data.SetFileDropList($files)
    $effect = $form.DoDragDrop($data, $Allowed)
    Set-Content "$env:TEMP\drag-result.txt" "effect=$effect"
    $form.Close()
})
$form.Add_Shown({ [Mover]::Start($form.Left + 110, $form.Top + 70, $X, $Y) })
$exit = New-Object System.Windows.Forms.Timer
$exit.Interval = 15000
$exit.Add_Tick({ $form.Close() })
$exit.Start()
[System.Windows.Forms.Application]::Run($form)

