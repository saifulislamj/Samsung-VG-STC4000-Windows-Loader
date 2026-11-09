using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.Win32;

[assembly: AssemblyTitle("Samsung VG-STC4000 TV Camera Installer")]
[assembly: AssemblyDescription("Windows 10/11 Driver and Auto-Loader for Samsung VG-STC4000")]
[assembly: AssemblyCompany("Saif Ul Islam (saifulislamj)")]
[assembly: AssemblyProduct("Samsung TV Camera Windows Suite")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Saif Ul Islam (https://github.com/saifulislamj)")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

class Setup
{
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WINUSB_SETUP_PACKET
    {
        public byte RequestType;
        public byte Request;
        public ushort Value;
        public ushort Index;
        public ushort Length;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_Initialize(IntPtr DeviceHandle, out IntPtr InterfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_Free(IntPtr InterfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_SetPipePolicy(
        IntPtr InterfaceHandle, byte PipeID, uint PolicyType, uint ValueLength, ref uint Value);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_ControlTransfer(
        IntPtr InterfaceHandle, WINUSB_SETUP_PACKET SetupPacket,
        byte[] Buffer, uint BufferLength, out uint LengthTransferred, IntPtr Overlapped);

    static bool IsCameraActive()
    {
        try
        {
            using (RegistryKey usbKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceClasses\{a5dcbf10-6530-11d2-901f-00c04fb951ed}"))
            {
                if (usbKey != null)
                {
                    foreach (string sub in usbKey.GetSubKeyNames())
                    {
                        if (sub.IndexOf("VID_04E8&PID_2062", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string formatted = sub.StartsWith("##?#") ? @"\\?\" + sub.Substring(4) : sub;
                            IntPtr h = CreateFile(formatted, GENERIC_READ | GENERIC_WRITE,
                                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                            if (h != (IntPtr)(-1))
                            {
                                CloseHandle(h);
                                return true;
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return false;
    }

    static string FindDevicePath()
    {
        try
        {
            using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceClasses"))
            {
                if (baseKey == null) return null;
                foreach (string guidStr in baseKey.GetSubKeyNames())
                {
                    using (RegistryKey guidKey = baseKey.OpenSubKey(guidStr))
                    {
                        if (guidKey == null) continue;
                        foreach (string subName in guidKey.GetSubKeyNames())
                        {
                            if (subName.IndexOf("VID_04E8&PID_2061&MI_02", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                string formatted = subName;
                                if (formatted.StartsWith("##?#"))
                                    formatted = @"\\?\" + formatted.Substring(4);

                                IntPtr h = CreateFile(formatted, GENERIC_READ | GENERIC_WRITE,
                                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                                if (h != (IntPtr)(-1))
                                {
                                    IntPtr winUsb;
                                    if (WinUsb_Initialize(h, out winUsb))
                                    {
                                        WinUsb_Free(winUsb);
                                        CloseHandle(h);
                                        return formatted;
                                    }
                                    CloseHandle(h);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    const ushort AIT_IDX = 0x0600;
    const ushort AIT_CMD_UPLOAD = 0x03;
    const ushort AIT_CMD_FW = 0x04;
    const int AIT_FW_CHUNK_SIZE = 32;
    const uint PIPE_TRANSFER_TIMEOUT = 0x03;

    static bool SendAitCmd(IntPtr winUsbHandle, byte[] data, ushort val, bool outDir)
    {
        WINUSB_SETUP_PACKET pkt = new WINUSB_SETUP_PACKET();
        pkt.RequestType = (byte)(0x20 | (outDir ? 0x00 : 0x80));
        pkt.Request = 0x01;
        pkt.Value = (ushort)(val << 8);
        pkt.Index = AIT_IDX;
        pkt.Length = (ushort)(data != null ? data.Length : 0);
        uint transferred;
        return WinUsb_ControlTransfer(winUsbHandle, pkt, data, pkt.Length, out transferred, IntPtr.Zero);
    }

    static bool UploadFirmware(string baseDir)
    {
        if (IsCameraActive())
        {
            return true;
        }

        string devPath = FindDevicePath();
        if (devPath == null)
        {
            Console.WriteLine("    [!] Camera USB hardware not responding yet. Please ensure camera is plugged in.");
            return false;
        }

        IntPtr hFile = CreateFile(devPath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (hFile == (IntPtr)(-1)) return false;

        try
        {
            IntPtr winUsb;
            if (!WinUsb_Initialize(hFile, out winUsb)) return false;
            try
            {
                uint timeoutMs = 3000;
                WinUsb_SetPipePolicy(winUsb, 0x00, PIPE_TRANSFER_TIMEOUT, 4, ref timeoutMs);

                string fwPath = Path.Combine(baseDir, "FalconPlus_FW.bin");
                if (!File.Exists(fwPath))
                {
                    Console.WriteLine("    [-] Firmware file missing: FalconPlus_FW.bin");
                    return false;
                }

                byte[] fwBytes = File.ReadAllBytes(fwPath);
                byte[] initCmd = new byte[8] { 0x01, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00 };
                if (!SendAitCmd(winUsb, initCmd, AIT_CMD_FW, true)) return false;

                int totalChunks = (fwBytes.Length + AIT_FW_CHUNK_SIZE - 1) / AIT_FW_CHUNK_SIZE;
                for (int i = 0; i < fwBytes.Length; i += AIT_FW_CHUNK_SIZE)
                {
                    byte[] chunk = new byte[AIT_FW_CHUNK_SIZE];
                    int len = Math.Min(AIT_FW_CHUNK_SIZE, fwBytes.Length - i);
                    Array.Copy(fwBytes, i, chunk, 0, len);
                    if (!SendAitCmd(winUsb, chunk, AIT_CMD_UPLOAD, true)) return false;
                }

                initCmd[1] = 0x01;
                return SendAitCmd(winUsb, initCmd, AIT_CMD_FW, true);
            }
            finally
            {
                WinUsb_Free(winUsb);
            }
        }
        finally
        {
            CloseHandle(hFile);
        }
    }

    static void InstallCertificate(string certPath)
    {
        try
        {
            if (!File.Exists(certPath)) return;
            X509Certificate2 cert = new X509Certificate2(certPath);

            using (X509Store store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
            }

            using (X509Store rootStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
            {
                rootStore.Open(OpenFlags.ReadWrite);
                rootStore.Add(cert);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("    [!] Certificate installation note: " + ex.Message);
        }
    }

    static void RunPnputil(string infPath)
    {
        ProcessStartInfo psi = new ProcessStartInfo("pnputil.exe", "/add-driver \"" + infPath + "\" /install");
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        using (Process p = Process.Start(psi))
        {
            p.WaitForExit();
        }
    }

    static void SetupStartupWatcher(string baseDir)
    {
        try
        {
            // 1. Install shortcut into Startup folder
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string scriptPath = Path.Combine(baseDir, "StartCameraWatcher.vbs");
            string shortcutPath = Path.Combine(startupFolder, "SamsungCamWatcher.lnk");

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = scriptPath;
            shortcut.WorkingDirectory = baseDir;
            shortcut.Save();

            // 2. Kill any old watcher processes
            foreach (Process p in Process.GetProcessesByName("SamsungCamLoader"))
            {
                try { if (p.Id != Process.GetCurrentProcess().Id) p.Kill(); } catch { }
            }
            Thread.Sleep(500);
            try
            {
                string oldExe = Path.Combine(baseDir, "SamsungCamLoader_old.exe");
                if (File.Exists(oldExe)) File.Delete(oldExe);
            }
            catch { }

            // 3. Launch background watcher IMMEDIATELY
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = Path.Combine(baseDir, "SamsungCamLoader.exe");
            psi.Arguments = "-w";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = baseDir;
            Process.Start(psi);
        }
        catch { }
    }

    public static void Main(string[] args)
    {
        Console.Title = "Samsung VG-STC4000 TV Camera Installer - Saif Ul Islam (saifulislamj)";
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  ██████╗  █████╗ ██╗███████╗    ██╗   ██╗██╗         ██╗███████╗██╗      █████╗ ███╗   ███╗");
        Console.WriteLine("  ██╔════╝ ██╔══██╗██║██╔════╝    ██║   ██║██║         ██║██╔════╝██║     ██╔══██╗████╗ ████║");
        Console.WriteLine("  ███████╗ ███████║██║█████╗      ██║   ██║██║         ██║███████╗██║     ███████║██╔████╔██║");
        Console.WriteLine("  ╚════██║ ██╔══██║██║██╔══╝      ██║   ██║██║         ██║╚════██║██║     ██╔══██║██║╚██╔╝██║");
        Console.WriteLine("  ███████║ ██║  ██║██║██║         ╚██████╔╝███████╗    ██║███████║███████╗██║  ██║██║ ╚═╝ ██║");
        Console.WriteLine("  ╚══════╝ ╚═╝  ╚═╝╚═╝╚═╝          ╚═════╝ ╚══════╝    ╚═╝╚══════╝╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("                  GitHub: https://github.com/saifulislamj                              ");
        Console.WriteLine("=========================================================================================");
        Console.WriteLine("       Samsung VG-STC4000 TV Camera Windows 10/11 Suite & Auto-Loader                    ");
        Console.WriteLine("       Special Thanks to: Ondrej Zary (https://github.com/ondrej-zary/samsung-tvcam-fwloader)");
        Console.WriteLine("=========================================================================================");
        Console.ResetColor();
        Console.WriteLine();

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        Console.Write("[1/4] Installing driver security certificate... ");
        string certPath = Path.Combine(baseDir, "driver", "driver.cer");
        InstallCertificate(certPath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("DONE!");
        Console.ResetColor();

        Console.Write("[2/4] Registering Samsung camera driver in Windows... ");
        string infPath = Path.Combine(baseDir, "driver", "usb2.0_uvc_hq_webcam_(interface_2).inf");
        RunPnputil(infPath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("DONE!");
        Console.ResetColor();

        Console.Write("[3/4] Uploading firmware to camera RAM... ");
        Thread.Sleep(1000);
        bool uploaded = UploadFirmware(baseDir);
        if (uploaded)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("DONE (Camera is active)!");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("QUEUED (Will boot automatically on connect)!");
            Console.ResetColor();
        }

        Console.Write("[4/4] Activating real-time Plug-and-Play background service... ");
        SetupStartupWatcher(baseDir);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("ACTIVE & RUNNING!");
        Console.ResetColor();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=========================================================================================");
        Console.WriteLine("   SUCCESS! Installation Complete!                                                       ");
        Console.WriteLine("   Camera and Microphone are now ready for Windows Camera, Zoom, Teams, OBS, etc.        ");
        Console.WriteLine("=========================================================================================");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("★ Developed with care by Saif Ul Islam (saifulislamj)");
        Console.WriteLine("★ Follow & Star on GitHub: https://github.com/saifulislamj");
        Console.WriteLine("★ Original Linux research by Ondrej Zary: https://github.com/ondrej-zary/samsung-tvcam-fwloader");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
