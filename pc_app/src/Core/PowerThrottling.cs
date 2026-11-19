using System;
using System.Runtime.InteropServices;

namespace GamepadEmulator.Core
{
    public static class PowerThrottling
    {
        private static bool s_isConfigured;

        public static void OptimizeProcessPowerSettings()
        {
            if (s_isConfigured) return;
            s_isConfigured = true;

            // 1. Request 1ms high resolution multimedia timer
            Win32Native.TimeBeginPeriod(1);

            // 2. Disable Windows 11 background power throttling for timer resolution
            try
            {
                Win32Native.PROCESS_POWER_THROTTLING_STATE state = new()
                {
                    Version = Win32Native.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = Win32Native.PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
                    StateMask = Win32Native.PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION
                };

                uint size = (uint)Marshal.SizeOf<Win32Native.PROCESS_POWER_THROTTLING_STATE>();
                IntPtr hProcess = Win32Native.GetCurrentProcess();

                // If running on Windows 10 or unsupported builds, SetProcessInformation will return false; safely ignore.
                Win32Native.SetProcessInformation(
                    hProcess,
                    Win32Native.ProcessPowerThrottling,
                    ref state,
                    size);
            }
            catch
            {
                // Silently fallback on older Windows environments
            }
        }
    }
}

