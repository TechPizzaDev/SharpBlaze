using System;
using System.Runtime.InteropServices;
using Silk.NET.SDL;
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
        else
        {
            SdlProvider.InitFlags = Sdl.InitVideo | Sdl.InitEvents;

            using Sdl sdl = Sdl.GetApi();
            using SdlMain main = new(sdl, width, height);
            main.Run();
        }
    }
}
