using System;
using System.Runtime.InteropServices;

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern IntPtr LoadLibraryW(string name);
[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
static extern IntPtr GetProcAddress(IntPtr h, string name);

string[] dlls = {
    "d3d11.dll", "dxgi.dll", "WinTypes.dll", "twinapi.appcore.dll",
    "combase.dll", "d3d11on12.dll", "Windows.UI.dll",
    "api-ms-win-gaming-deviceinformation-l1-1-0.dll",
    "ext-ms-win-graphicscapture-l1-1-0.dll",
    @"C:\Windows\System32\d3d11.dll",
    @"C:\Windows\System32\dxgi.dll",
};

foreach (var dll in dlls)
{
    var h = LoadLibraryW(dll);
    if (h != IntPtr.Zero)
    {
        var p = GetProcAddress(h, "CreateDirect3D11DeviceFromDXGIDevice");
        Console.WriteLine($"{dll}: loaded=YES, CreateDirect3D11DeviceFromDXGIDevice={p}");
    }
    else
    {
        Console.WriteLine($"{dll}: loaded=NO, error={Marshal.GetLastWin32Error()}");
    }
}
