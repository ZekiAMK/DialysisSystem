// NativeMethods.cs
using System.Runtime.InteropServices;

namespace MAUIAapp.Control;

internal static class NativeMethods
{
    private const string Lib = "main";

    [DllImport(Lib)] public static extern void Game_Init(string wadPath);
    [DllImport(Lib)] public static extern void Game_Tick();
    [DllImport(Lib)] public static extern void Game_Stop();

}
