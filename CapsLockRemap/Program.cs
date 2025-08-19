using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;


namespace CapsLockRemap
{
    internal class Program
    {
        private static NotifyIcon _trayIcon;

        private static string TargetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".pargivaht", "CAPSpotify");

        private static string TargetExe => Path.Combine(TargetDir, "CAPSpotify.exe");


        // Windows API constants
        const int WM_APPCOMMAND = 0x319;
        const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;
        const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
        const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;


        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        const int WM_SYSKEYDOWN = 0x0104; // Alt + key triggers this
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;


        static string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".pargivaht", "CAPSpotify", "config.json");

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(Keys vKey);


        public static void Main()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Directory.SetCurrentDirectory(exeDir);


            if (!File.Exists(configPath))
                OpenConfig();
            CreateTrayIcon();
            _ = SpotifyAuth.Init();
            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);

        }

        public static void Restart()
        {
            string exePath = Application.ExecutablePath;
            Process.Start(exePath);
            Application.Exit();
        }

        public static void InstallAndRestartToAppData()
        {
            try
            {
                string currentExe = Assembly.GetExecutingAssembly().Location;

                Directory.CreateDirectory(TargetDir);

                if (string.Equals(currentExe, TargetExe, StringComparison.OrdinalIgnoreCase))
                {
                    AddToStartup(TargetExe);
                    return;
                }

                File.Copy(currentExe, TargetExe, true);

                AddToStartup(TargetExe);

                Process.Start(TargetExe);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to install to AppData: " + ex.Message);
            }
        }

        private static void AddToStartup(string exePath)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                key.SetValue("CAPSpotify", exePath);
            }
        }

        private static void CreateTrayIcon()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            _trayIcon.Visible = true;
            _trayIcon.Text = "CAPSpotify";

            // Create tray menu  
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open config", null, (s, e) => OpenConfig());
            contextMenu.Items.Add(new ToolStripSeparator());
            if (!Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".pargivaht", "CAPSpotify"), StringComparison.OrdinalIgnoreCase))
                contextMenu.Items.Add("Add to startup", null, (s, e) => InstallAndRestartToAppData());
            contextMenu.Items.Add("Restart", null, (s, e) => Restart());
            contextMenu.Items.Add("Exit", null, (s, e) => Application.Exit());


            _trayIcon.ContextMenuStrip = contextMenu;
        }

        public static void OpenConfig()
        {
            Process.Start("notepad.exe", configPath);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {

            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                bool ctrl = (GetAsyncKeyState(Keys.ControlKey) & 0x8000) != 0;
                bool shift = (GetAsyncKeyState(Keys.ShiftKey) & 0x8000) != 0;
                bool alt = (GetAsyncKeyState(Keys.Menu) & 0x8000) != 0;

                if (vkCode == (int)Keys.CapsLock)
                {
                    if (ctrl && shift)
                        spotifyControl(4);
                    else if (ctrl)
                        spotifyControl(3);
                    else if (shift)
                        spotifyControl(2);
                    else if (alt)
                        spotifyControl(1);
                    else
                        spotifyControl(0);

                    return (IntPtr)1; // block CapsLock
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }




        static void spotifyControl(int option)
        {
            Process spotifyProcess = FindSpotifyProcess();

            if (spotifyProcess != null)
            {
                IntPtr hwnd = spotifyProcess.MainWindowHandle;

                if (hwnd != IntPtr.Zero)
                {
                    switch (option)
                    {
                        case 0:
                            SendMessageW(hwnd, WM_APPCOMMAND, IntPtr.Zero, (IntPtr)(APPCOMMAND_MEDIA_PLAY_PAUSE << 16));
                            break;
                        case 1:
                            _ = SpotifyAuth.LikeCurrentSong();
                            break;
                        case 2:
                            SendMessageW(hwnd, WM_APPCOMMAND, IntPtr.Zero, (IntPtr)(APPCOMMAND_MEDIA_NEXTTRACK << 16));
                            break;
                        case 3:
                            SendMessageW(hwnd, WM_APPCOMMAND, IntPtr.Zero, (IntPtr)(APPCOMMAND_MEDIA_PREVIOUSTRACK << 16));
                            break;
                        case 4:
                            //TODO: Volume control.
                            break;

                    }
                }
                else
                {
                    Console.WriteLine("hwd is IntPtr.Zero :(");
                }
            }
            else
            {
                Console.WriteLine("Spotify not running");
            }
        }

        static Process FindSpotifyProcess()
        {
            foreach (var process in Process.GetProcessesByName("Spotify"))
            {
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                    return process;
            }
            return null;
        }




        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
