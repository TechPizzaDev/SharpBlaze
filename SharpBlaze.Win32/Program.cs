using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace SharpBlaze.Win32;

internal class Program
{
    static void Main(string[] args)
    {
        Win32Main main = new(1280, 720);
        nint hinstance = Marshal.GetHINSTANCE(typeof(Main).Module);
        main.WinMain((HINSTANCE)hinstance);
    }
}