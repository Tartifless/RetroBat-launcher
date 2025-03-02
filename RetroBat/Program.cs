using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using static RetroBat.IniFileReader;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Threading;
using System.Reflection;

namespace RetroBat
{
    class Program
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(uint dwProcessId);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        const int SW_RESTORE = 9;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOACTIVATE = 0x0010;

        static void Main()
        {
            string dependenciesFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system", "dependencies");

            string appFolder = Directory.GetCurrentDirectory();

            string iniPath = Path.Combine(appFolder, "retrobat.ini"); // Ensure this file is next to your executable

            if (!File.Exists(iniPath))
            {
                LogToFile("[WARNING] retrobat.ini not found");
                return;
            }

            // Write values to registry
            SetRegistryKey(appFolder + "\\");

            // Get values from ini file
            IniFileReader iniReader = new(iniPath);
            LogToFile("[INFO] Reading retrobat.ini.");

            RetroBatConfig config = new()
            {
                LanguageDetection = ReadInt(iniReader, "RetroBat", "LanguageDetection", 0),
                ResetConfigMode = ReadBool(iniReader, "RetroBat", "ResetConfigMode", false),
                Autostart = ReadBool(iniReader, "RetroBat", "Autostart", false),
                WiimoteGun = ReadBool(iniReader, "RetroBat", "WiimoteGun", false),

                EnableIntro = ReadBool(iniReader, "SplashScreen", "EnableIntro", true),
                FileName = ReadValue("SplashScreen", "FileName", "RetroBat-neon.mp4"),
                FilePath = ReadValue("SplashScreen", "FilePath", "default"),
                RandomVideo = ReadBool(iniReader, "SplashScreen", "RandomVideo", true),
                VideoDuration = ReadInt(iniReader, "SplashScreen", "VideoDuration", 15000),

                Fullscreen = ReadBool(iniReader, "EmulationStation", "Fullscreen", true),
                ForceFullscreenRes = ReadBool(iniReader, "EmulationStation", "ForceFullscreenRes", false),
                GameListOnly = ReadBool(iniReader, "EmulationStation", "GameListOnly", false),
                InterfaceMode = ReadInt(iniReader, "EmulationStation", "InterfaceMode", 0),
                MonitorIndex = ReadInt(iniReader, "EmulationStation", "MonitorIndex", 0),
                NoExitMenu = ReadBool(iniReader, "EmulationStation", "NoExitMenu", false),
                WindowXSize = ReadInt(iniReader, "EmulationStation", "WindowXSize", 1280),
                WindowYSize = ReadInt(iniReader, "EmulationStation", "WindowYSize", 720)
            };

            // Get emulationstation.exe path
            string esPath = Path.Combine(appFolder, "emulationstation");
            string emulationStationExe = Path.Combine(esPath, "emulationstation.exe");

            if (!File.Exists(emulationStationExe))
            {
                LogToFile("[ERROR] Emulationstation executable not found in: " + emulationStationExe);
                return;
            }

            // Play video intro
            if (config.EnableIntro)
                RunRetroBatVideo(config, appFolder, emulationStationExe, esPath);

            // Arguments
            LogToFile("[INFO] Setting up arguments to run EmulationStation.");
            List<string> commandArray = [];

            if (config.Fullscreen && config.ForceFullscreenRes)
            {
                commandArray.Add("--resolution");
                commandArray.Add(config.WindowXSize.ToString());
                commandArray.Add(config.WindowYSize.ToString());
            }

            else if (!config.Fullscreen)
            {
                commandArray.Add("--windowed");
                commandArray.Add("--resolution");
                commandArray.Add(config.WindowXSize.ToString());
                commandArray.Add(config.WindowYSize.ToString());
            }

            if (config.GameListOnly)
                commandArray.Add("--gamelist-only");

            if (config.InterfaceMode == 2)
                commandArray.Add("--force-kid");
            else if (config.InterfaceMode == 1)
                commandArray.Add("--force-kiosk");

            if (config.MonitorIndex > 0)
            {
                commandArray.Add("--monitor");
                commandArray.Add(config.MonitorIndex.ToString());
            }

            if (config.NoExitMenu)
                commandArray.Add("--no-exit");

            commandArray.Add("--home");
            commandArray.Add(esPath);

            string args = string.Join(" ", commandArray);

            // Run EmulationStation
            LogToFile("[INFO] Running " + emulationStationExe + " " + args);

            var start = new ProcessStartInfo()
            {
                FileName = emulationStationExe,
                WorkingDirectory = esPath,
                Arguments = args,
            };

            if (start == null)
                return;

            var process = Process.Start(start);
            

            if (process != null)
            {
                Thread.Sleep(1000);
                BringWindowToFront(process);

                process?.WaitForExit();
            }
        }

        class RetroBatConfig
        {
            public int LanguageDetection { get; set; }
            public bool ResetConfigMode { get; set; }
            public bool Autostart { get; set; }
            public bool WiimoteGun { get; set; }

            public bool EnableIntro { get; set; }
            public string? FileName { get; set; }
            public string? FilePath { get; set; }
            public bool RandomVideo { get; set; }
            public int VideoDuration { get; set; }

            public bool Fullscreen { get; set; }
            public bool ForceFullscreenRes { get; set; }
            public bool GameListOnly { get; set; }
            public int InterfaceMode { get; set; }
            public int MonitorIndex { get; set; }
            public bool NoExitMenu { get; set; }
            public int WindowXSize { get; set; }
            public int WindowYSize { get; set; }
        }

        static void LogToFile(string message)
        {
            string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "retrobat.log");

            using (StreamWriter writer = new(logFilePath, append: true))
                writer.WriteLine($"{DateTime.Now}: {message}");
        }

        public static void SetRegistryKey(string appFolder)
        {
            LogToFile("[INFO] Writing values to registry.");

            string registryPath = @"SOFTWARE\RetroBat";
            string ftpPath = "InstallRootUrl";
            string installPath = "LatestKnownInstallPath";

            string urlValue = "http://www.retrobat.ovh/repo/win64";

            using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(registryPath, true))
            {
                if (registryKey.GetValue(ftpPath) == null)
                    registryKey.SetValue(ftpPath, urlValue);
                else
                {
                    string currentFTPPath = registryKey.GetValue(ftpPath).ToString();
                    if (currentFTPPath != urlValue)
                        registryKey.SetValue(ftpPath, urlValue);
                }

                if (registryKey.GetValue(installPath) == null)
                    registryKey.SetValue(installPath, appFolder);
                else
                {
                    string currentInstallPath = registryKey.GetValue(installPath).ToString();
                    if (currentInstallPath != appFolder)
                        registryKey.SetValue(installPath, appFolder);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static void RunRetroBatVideo(RetroBatConfig config, string appFolder, string emulationStationExe, string esPath)
        {
            LogToFile("[INFO] Trying to load splash video.");

            string videoFilePath = Path.Combine(appFolder, "emulationstation", ".emulationstation", "video");

            if (config.FilePath != "default")
                videoFilePath = config.FilePath;

            if (!Directory.Exists(videoFilePath))
                return;

            var videoFiles = Directory.EnumerateFiles(videoFilePath, "*.mp4").ToList();
            if (videoFiles.Count == 0)
                return;

            string videoFile = config.FileName ?? "retrobat-neon.mp4";
            if (config.RandomVideo)
            {
                Random rand = new Random();
                videoFile = videoFiles[rand.Next(videoFiles.Count)];
            }

            string video = Path.Combine(videoFilePath, videoFile);

            if (!File.Exists(video))
                return;

            LogToFile("[INFO] Loading splashcreen video: " + video);

            var p = new ProcessStartInfo()
            {
                FileName = emulationStationExe,
                WorkingDirectory = esPath,
                Arguments = "--video " + "\"" + video + "\"",
            };

            var process = Process.Start(p);

            if (process != null)
            {
                int elapsed = 0;
                const int checkInterval = 100;
                int duration = config.VideoDuration;

                Thread.Sleep(1000);

                while (!process.HasExited && elapsed < duration)
                {
                    Thread.Sleep(checkInterval);
                    elapsed += checkInterval;

                    // Check for keyboard or mouse input
                    if (KeyboardOrMousePressed())
                    {
                        LogToFile("[INFO] video killed by button press.");
                        process.Kill();
                        process.Dispose();
                        return;
                    }
                }
                if (!process.HasExited)
                {
                    process.Kill();
                    process.Dispose();
                }
            }
        }

        private const int VK_SPACE = 0x20;
        private const int VK_RETURN = 0x0D;
        private const int VK_LBUTTON = 0x01;  // Left Mouse Button
        private const int VK_RBUTTON = 0x02;  // Right Mouse Button
        private const int VK_MBUTTON = 0x04;  // Middle Mouse Button

        private static bool KeyboardOrMousePressed()
        {
            if ((GetAsyncKeyState(VK_SPACE) & 0x8000) != 0)
                return true;
            if ((GetAsyncKeyState(VK_RETURN) & 0x8000) != 0)
                return true;
            if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
                return true;
            if ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0)
                return true;
            if ((GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0)
                return true;

            return false;
        }

        static void BringWindowToFront(Process process)
        {
            IntPtr hWnd = process.MainWindowHandle;
            
            AllowSetForegroundWindow((uint)process.Id);

            ShowWindow(hWnd, SW_RESTORE);

            //SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOACTIVATE);

            for (int i = 0; i < 5; i++)
            {
                SetFocus(hWnd);
                SetForegroundWindow(hWnd);
                Thread.Sleep(100); // Small delay between attempts
            }
        }
    }
}