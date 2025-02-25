using System;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace SharpBlaze.Win32;

internal class Program
{
    static void Main(string[] args)
    {
        uint width = 1280;
        uint height = 720;

        if (OperatingSystem.IsWindows())
        {
            Win32Main main = new(width, height);
            nint hinstance = Marshal.GetHINSTANCE(typeof(Program).Module);
            main.WinMain((HINSTANCE)hinstance);
        }
    }
}
