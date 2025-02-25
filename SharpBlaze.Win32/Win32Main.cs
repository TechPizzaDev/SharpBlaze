using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Versioning;
using TerraFX.Interop.Windows;

namespace SharpBlaze.Win32;

using static VK;
using static Windows;

[SupportedOSPlatform("windows")]
public unsafe class Win32Main : Main
{
    private static readonly ConcurrentDictionary<IntPtr, Win32Main> hwndToInstanceLookup = new();

    private HBITMAP g_hBitmap;

    private long lastPaint = Stopwatch.GetTimestamp();

    public Win32Main(uint width, uint height) : base(width, height)
    {
    }

    public int WinMain(HINSTANCE hInstance)
    {
        ushort windowClass;
        fixed (char* className = "RasterizerWindow")
        {
            WNDCLASSEXW wcex = new();
            wcex.cbSize = (uint) sizeof(WNDCLASSEXW);

            wcex.style = CS.CS_HREDRAW | CS.CS_VREDRAW;
            wcex.lpfnWndProc = &WndProc;
            wcex.hInstance = hInstance;
            wcex.hIcon = LoadIcon(default, IDI.IDI_APPLICATION);
            wcex.hCursor = LoadCursor(default, IDC.IDC_ARROW);
            wcex.hbrBackground = (HBRUSH) GetStockObject(BLACK_BRUSH);
            wcex.lpszClassName = className;

            windowClass = RegisterClassExW(&wcex);
        }

        HWND hWnd;
        fixed (char* windowName = "Rasterizer")
        {
            void* mainHandle = (void*) GCHandle.ToIntPtr(GCHandle.Alloc(this));
            hWnd = CreateWindowW((char*) windowClass, windowName, WS.WS_SYSMENU,
                CW_USEDEFAULT, CW_USEDEFAULT, (int) WindowWidth, (int) WindowHeight, default, default, hInstance, mainHandle);
        }

        HDC hdc = GetDC(hWnd);
        g_hBitmap = CreateCompatibleBitmap(hdc, (int) WindowWidth, (int) WindowHeight);
        ReleaseDC(hWnd, hdc);

        ShowWindow(hWnd, SW.SW_SHOW);
        UpdateWindow(hWnd);

        MSG msg;
        while (GetMessage(&msg, default, 0, 0))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        return 0;
    }

    [UnmanagedCallersOnly]
    private static LRESULT WndProc(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        switch (message)
        {
            case WM.WM_CREATE:
            {
                CREATESTRUCTW* createInfo = (CREATESTRUCTW*) lParam.Value;
                GCHandle handle = GCHandle.FromIntPtr((IntPtr) createInfo->lpCreateParams);
                hwndToInstanceLookup[(IntPtr) hWnd.Value] = (Win32Main) handle.Target!;
                handle.Free();
                break;
            }

            case WM.WM_PAINT:
            {
                Win32Main main = hwndToInstanceLookup[(IntPtr) hWnd.Value];
                main.Rasterize();

                (double avgRasterTime, double stDev, double median) = main.GetTimings();

                int fps = (int) (1000.0f / avgRasterTime);

                string title = $"FPS: {fps}      Rasterization time: {avgRasterTime:0.000}±{stDev:0.000}ms stddev / {median:0.000}ms median";
                fixed (char* titlePtr = title)
                {
                    SetWindowText(hWnd, titlePtr);
                }

                //main.g_rasterizer.readBackDepth(main.g_rawData);
                var image = main.mImage;

                PAINTSTRUCT ps;
                HDC hdc = BeginPaint(hWnd, &ps);

                HDC hdcMem = CreateCompatibleDC(hdc);

                BITMAPINFO info = new();
                info.bmiHeader.biSize = (uint) sizeof(BITMAPINFOHEADER);
                info.bmiHeader.biWidth = image.GetImageWidth();
                info.bmiHeader.biHeight = -image.GetImageHeight();
                info.bmiHeader.biPlanes = 1;
                info.bmiHeader.biBitCount = 32;
                info.bmiHeader.biCompression = BI.BI_RGB;

                int byteCount = image.GetBytesPerRow() * image.GetImageHeight();
                byte* pixelStart = image.GetImageData();
                SwapRedAndBlue(new Span<byte>(pixelStart, byteCount));
                SetDIBits(hdcMem, main.g_hBitmap, 0, (uint) image.GetImageHeight(), pixelStart, &info, DIB_RGB_COLORS);

                BITMAP bm;
                HGDIOBJ hbmOld = SelectObject(hdcMem, main.g_hBitmap);

                GetObject(main.g_hBitmap, sizeof(BITMAP), &bm);

                BitBlt(hdc, 0, 0, bm.bmWidth, bm.bmHeight, hdcMem, 0, 0, SRCCOPY);

                SelectObject(hdcMem, hbmOld);
                DeleteDC(hdcMem);

                EndPaint(hWnd, &ps);

                long now = Stopwatch.GetTimestamp();

                float deltaTime = (float) Stopwatch.GetElapsedTime(main.lastPaint, now).TotalMilliseconds;
                float translateSpeed = 0.1f * deltaTime;
                float rotateSpeed = 0.002f * deltaTime;
                float zoomSpeed = 0.001f * deltaTime;

                main.lastPaint = now;

                if (GetAsyncKeyState(VK_SHIFT) != 0)
                    translateSpeed *= 3.0f;

                if (GetAsyncKeyState(VK_CONTROL) != 0)
                    translateSpeed *= 0.1f;

                if (GetAsyncKeyState('W') != 0)
                    main.mViewData.Translation += new FloatPoint(0, translateSpeed);

                if (GetAsyncKeyState('S') != 0)
                    main.mViewData.Translation -= new FloatPoint(0, translateSpeed);

                if (GetAsyncKeyState('A') != 0)
                    main.mViewData.Translation += new FloatPoint(translateSpeed, 0);

                if (GetAsyncKeyState('D') != 0)
                    main.mViewData.Translation -= new FloatPoint(translateSpeed, 0);

                if (GetAsyncKeyState(VK_UP) != 0)
                    main.mViewData.Scale += new FloatPoint(zoomSpeed);

                if (GetAsyncKeyState(VK_DOWN) != 0)
                    main.mViewData.Scale -= new FloatPoint(zoomSpeed);

                InvalidateRect(hWnd, null, FALSE);
                break;
            }

            case WM.WM_DESTROY:
                PostQuitMessage(0);
                break;

            default:
                return DefWindowProc(hWnd, message, wParam, lParam);
        }
        return 0;
    }

    private static void SwapRedAndBlue(Span<byte> data)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            while (data.Length >= Vector128<byte>.Count)
            {
                Vector128<byte> rgba = Vector128.Create<byte>(data);
                Vector128<byte> bgra = Vector128.Shuffle(
                    rgba,
                    Vector128.Create((byte) 2, 1, 0, 3, 6, 5, 4, 7, 10, 9, 8, 11, 14, 13, 12, 15));
                
                bgra.CopyTo(data);
                data = data.Slice(Vector128<byte>.Count);
            }
        }

        while (data.Length >= 4)
        {
            byte r = data[2];
            byte b = data[0];

            data[0] = r;
            data[2] = b;

            data = data.Slice(4);
        }
    }
}
