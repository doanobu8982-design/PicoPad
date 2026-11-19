using System;
using System.Runtime.InteropServices;

namespace GamepadEmulator.Core
{
    public static class Win32Native
    {
        public const int RIDEV_INPUTSINK = 0x00000100;
        public const int RIDEV_REMOVE = 0x00000001;

        public const uint RID_INPUT = 0x10000003;
        public const uint RID_HEADER = 0x10000005;

        public const int RIM_TYPEMOUSE = 0;
        public const int RIM_TYPEKEYBOARD = 1;
        public const int RIM_TYPEHID = 2;

        public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        public const uint WM_INPUT = 0x00FF;
        public const uint WM_DESTROY = 0x0002;
        public const uint WM_CLOSE = 0x0010;
        public const uint WM_QUIT = 0x0012;
        public const uint WM_USER = 0x0400;

        // Device change messages
        public const uint WM_DEVICECHANGE = 0x0219;
        public const uint DBT_DEVICEARRIVAL = 0x8000;
        public const uint DBT_DEVICEREMOVECOMPLETE = 0x8004;
        public const uint DBT_DEVTYP_PORT = 0x00000003;

        public const ushort RI_KEY_MAKE = 0;
        public const ushort RI_KEY_BREAK = 1;
        public const ushort RI_KEY_E0 = 2;
        public const ushort RI_KEY_E1 = 4;

        public const uint PM_REMOVE = 0x0001;
        public const uint PM_NOREMOVE = 0x0000;

        public const uint QS_ALLINPUT = 0x04FF;
        public const uint MWMO_INPUTAVAILABLE = 0x0004;

        public const uint WAIT_OBJECT_0 = 0x00000000;
        public const uint WAIT_TIMEOUT = 0x00000102;
        public const uint WAIT_FAILED = 0xFFFFFFFF;

        // File & Serial constants
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public const uint SETDTR = 5;
        public const uint CLRDTR = 6;
        public const uint SETRTS = 3;
        public const uint CLRRTS = 4;

        // Power throttling constants
        public const int ProcessPowerThrottling = 0x4;
        public const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        public const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
        public const uint PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COMMTIMEOUTS
        {
            public uint ReadIntervalTimeout;
            public uint ReadTotalTimeoutMultiplier;
            public uint ReadTotalTimeoutConstant;
            public uint WriteTotalTimeoutMultiplier;
            public uint WriteTotalTimeoutConstant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RAWINPUT
        {
            [FieldOffset(0)]
            public RAWINPUTHEADER header;

            [FieldOffset(16)]
            public RAWKEYBOARD keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUT64
        {
            public RAWINPUTHEADER header;
            public RAWKEYBOARD keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
            public uint lPrivate;
        }

        public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterRawInputDevices(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint MsgWaitForMultipleObjectsEx(
            uint nCount,
            IntPtr[]? pHandles,
            uint dwMilliseconds,
            uint dwWakeMask,
            uint dwFlags);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint uMilliseconds);

        // Win32 Serial & Handle APIs
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool EscapeCommFunction(IntPtr hFile, uint dwFunc);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetCommTimeouts(IntPtr hFile, [In] ref COMMTIMEOUTS lpCommTimeouts);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FlushFileBuffers(IntPtr hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern unsafe bool WriteFile(
            IntPtr hFile,
            byte* lpBuffer,
            uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetProcessInformation(
            IntPtr hProcess,
            int ProcessInformationClass,
            ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
            uint ProcessInformationSize);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();
    }
}
