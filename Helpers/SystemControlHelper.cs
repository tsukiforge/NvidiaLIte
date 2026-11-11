using System;
using System.Diagnostics;
using System.Management;
using NAudio.CoreAudioApi;

namespace NvidiaCi
{
    public static class SystemControlHelper
    {
        // --- VOLUME CONTROL (Precise with NAudio) ---
        public static float GetSystemVolume()
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return device.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
        }

        public static void SetSystemVolume(float level)
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(level, 0, 100) / 100f;
        }

        // --- BRIGHTNESS CONTROL (Via WMI) ---
        public static int GetBrightness()
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                using (obj)
                    return (byte)obj.GetPropertyValue("CurrentBrightness");
            }

            throw new NotSupportedException("Monitor ini tidak menyediakan kontrol brightness melalui Windows WMI.");
        }

        public static void SetBrightness(int level)
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                using (obj)
                {
                    obj.InvokeMethod("WmiSetBrightness", new object[] { uint.MaxValue, (byte)Math.Clamp(level, 0, 100) });
                    return;
                }
            }

            throw new NotSupportedException("Monitor ini tidak mendukung perubahan brightness melalui Windows WMI.");
        }

        public static void OpenWifiSettings()
        {
            Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true });
        }
    }
}
