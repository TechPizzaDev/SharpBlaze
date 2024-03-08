using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace SharpBlaze.Win32;

using static VK;
using static Windows;

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

                PAINTSTRUCT ps;
                HDC hdc = BeginPaint(hWnd, &ps);

                HDC hdcMem = CreateCompatibleDC(hdc);

                BITMAPINFO info = new();
                info.bmiHeader.biSize = (uint) sizeof(BITMAPINFOHEADER);
                info.bmiHeader.biWidth = (int) main.WindowWidth;
                info.bmiHeader.biHeight = (int) main.WindowHeight;
                info.bmiHeader.biPlanes = 1;
                info.bmiHeader.biBitCount = 32;
                info.bmiHeader.biCompression = BI.BI_RGB;
                SetDIBits(hdcMem, main.g_hBitmap, 0, main.WindowHeight, main.g_rawData, &info, DIB_RGB_COLORS);

                BITMAP bm;
                HGDIOBJ hbmOld = SelectObject(hdcMem, main.g_hBitmap);

                GetObject(main.g_hBitmap, sizeof(BITMAP), &bm);

                BitBlt(hdc, 0, 0, bm.bmWidth, bm.bmHeight, hdcMem, 0, 0, SRCCOPY);

                SelectObject(hdcMem, hbmOld);
                DeleteDC(hdcMem);

                EndPaint(hWnd, &ps);

                long now = Stopwatch.GetTimestamp();

                //Vector3 right = Vector3.Normalize(Vector3.Cross(main.g_cameraDirection, main.g_upVector));
                float deltaTime = (float) Stopwatch.GetElapsedTime(main.lastPaint, now).TotalMilliseconds;
                float translateSpeed = 0.01f * deltaTime;
                float rotateSpeed = 0.002f * deltaTime;

                main.lastPaint = now;

                if (GetAsyncKeyState(VK_SHIFT) != 0)
                    translateSpeed *= 3.0f;

                if (GetAsyncKeyState(VK_CONTROL) != 0)
                    translateSpeed *= 0.1f;

                //if (GetAsyncKeyState('W') != 0)
                //    main.g_cameraPosition = Vector3.Add(main.g_cameraPosition, Vector3.Multiply(main.g_cameraDirection, translateSpeed));
                //
                //if (GetAsyncKeyState('S') != 0)
                //    main.g_cameraPosition = Vector3.Add(main.g_cameraPosition, Vector3.Multiply(main.g_cameraDirection, -translateSpeed));
                //
                //if (GetAsyncKeyState('A') != 0)
                //    main.g_cameraPosition = Vector3.Add(main.g_cameraPosition, Vector3.Multiply(right, translateSpeed));
                //
                //if (GetAsyncKeyState('D') != 0)
                //    main.g_cameraPosition = Vector3.Add(main.g_cameraPosition, Vector3.Multiply(right, -translateSpeed));
                //
                //if (GetAsyncKeyState(VK_UP) != 0)
                //    main.g_cameraDirection = Vector3.Transform(main.g_cameraDirection, Quaternion.CreateFromAxisAngle(right, rotateSpeed));
                //
                //if (GetAsyncKeyState(VK_DOWN) != 0)
                //    main.g_cameraDirection = Vector3.Transform(main.g_cameraDirection, Quaternion.CreateFromAxisAngle(right, -rotateSpeed));
                //
                //if (GetAsyncKeyState(VK_LEFT) != 0)
                //    main.g_cameraDirection = Vector3.Transform(main.g_cameraDirection, Quaternion.CreateFromAxisAngle(main.g_upVector, -rotateSpeed));
                //
                //if (GetAsyncKeyState(VK_RIGHT) != 0)
                //    main.g_cameraDirection = Vector3.Transform(main.g_cameraDirection, Quaternion.CreateFromAxisAngle(main.g_upVector, rotateSpeed));
                //
                //if ((GetAsyncKeyState('R') & 1) != 0)
                //{
                //    main.CycleRasterizerImpl();
                //}

                InvalidateRect(hWnd, default, FALSE);
            }
            break;

            case WM.WM_DESTROY:
                PostQuitMessage(0);
                break;

            default:
                return DefWindowProc(hWnd, message, wParam, lParam);
        }
        return 0;
    }
}
