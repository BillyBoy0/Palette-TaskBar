// Palette-TaskBar : jusqu'à 6 ronds de couleur dans la zone de notification ; quitter les remplace par une icône palette.
// Clic sur un rond = copie le code hex, clic droit = changer de palette, gérer les palettes, quitter.
// Compiler avec build.bat (compilateur C# fourni avec Windows, C# 5).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Palette-TaskBar")]
[assembly: AssemblyProduct("Palette-TaskBar")]
[assembly: AssemblyDescription("Palettes de couleurs dans la barre des tâches")]
[assembly: AssemblyCompany("Cyril Neau")]
[assembly: AssemblyCopyright("© 2026 Cyril Neau")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace PaletteTaskBar
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int processId);

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch (Exception) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsWindows11())
            {
                MessageBox.Show("Palette-TaskBar nécessite Windows 11.\nPalette-TaskBar requires Windows 11.",
                    "Palette-TaskBar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Lang.Load();
            if (args.Length > 0 && args[0] == "/uninstall") { Installer.Uninstall(); return; }
            if (!Installer.IsInstalledCopy()) { Installer.Install(); return; }

            bool first;
            using (Mutex mutex = new Mutex(true, Signals.MutexName, out first))
            {
                if (!first)
                {
                    // Déjà lancé (clic sur l'icône épinglée) : l'instance en cours ouvre l'éditeur au premier plan.
                    AllowSetForegroundWindow(-1);
                    Signals.Send(Signals.OpenName);
                    return;
                }
                Application.Run(new TrayApp());
            }
        }

        // Windows 11 = build 22000 et plus. Environment.OSVersion ment sans manifeste, d'où la lecture du registre.
        static bool IsWindows11()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    int build;
                    string value = k != null ? k.GetValue("CurrentBuildNumber") as string : null;
                    return value == null || !int.TryParse(value, out build) || build >= 22000;
                }
            }
            catch (Exception) { return true; }
        }
    }

    // ---------- Langue ----------

    // Choisie à l'installation et rangée dans le registre ; à défaut, celle de Windows.
    static class Lang
    {
        const string Key = @"Software\Palette-TaskBar";
        public static bool English;

        public static string T(string fr, string en)
        {
            return English ? en : fr;
        }

        public static void Load()
        {
            string value = null;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key))
                    if (k != null) value = k.GetValue("Language") as string;
            }
            catch (Exception) { }
            English = value != null ? value == "en" : !SystemIsFrench();
        }

        public static bool SystemIsFrench()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";
        }

        public static void Save(bool english)
        {
            English = english;
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Key))
                k.SetValue("Language", english ? "en" : "fr");
        }

        public static void Delete()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(Key, false); } catch (Exception) { }
        }
    }

    // ---------- Données ----------

    public class Palette
    {
        public string name { get; set; }
        public List<string> colors { get; set; }
    }

    public class Config
    {
        public string active { get; set; }
        // Ronds masqués : seule l'icône palette reste dans la barre.
        public bool hidden { get; set; }
        public List<Palette> palettes { get; set; }

        public Palette Active()
        {
            foreach (Palette p in palettes)
                if (p.name == active) return p;
            return palettes.Count > 0 ? palettes[0] : null;
        }
    }

    static class Store
    {
        static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        static readonly string Folder = Path.Combine(AppData, "Palette-TaskBar");
        public static readonly string FilePath = Path.Combine(Folder, "palettes.json");
        // Emplacements des premières versions (nommées PaletteBarre) : le fichier est repris une fois.
        static readonly string[] OldPaths =
        {
            Path.Combine(Path.Combine(AppData, "PaletteBarre"), "palettes.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "palettes.json")
        };

        public static void DeleteAll()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
        }

        public static Config Load()
        {
            Directory.CreateDirectory(Folder);
            foreach (string old in OldPaths)
            {
                if (File.Exists(FilePath) || !File.Exists(old)) continue;
                File.Copy(old, FilePath);
                try { File.Delete(old); } catch (Exception) { }
            }

            if (File.Exists(FilePath))
            {
                try
                {
                    Config c = new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (c != null)
                    {
                        Clean(c);
                        if (c.palettes.Count > 0) return c;
                    }
                }
                catch (Exception ex)
                {
                    // Fichier abîmé : on ne l'écrase pas, on travaille avec l'exemple en mémoire.
                    MessageBox.Show(Lang.T("palettes.json est illisible, la palette d'exemple est utilisée en attendant.",
                                           "palettes.json can't be read, the example palette is used in the meantime.") +
                                    "\n\n" + ex.Message,
                        "Palette-TaskBar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Example();
                }
            }
            Config fresh = Example();
            Save(fresh);
            return fresh;
        }

        public static void Save(Config c)
        {
            Directory.CreateDirectory(Folder);
            JavaScriptSerializer js = new JavaScriptSerializer();
            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n  \"active\": ").Append(js.Serialize(c.active))
              .Append(",\r\n  \"hidden\": ").Append(c.hidden ? "true" : "false")
              .Append(",\r\n  \"palettes\": [\r\n");
            for (int i = 0; i < c.palettes.Count; i++)
            {
                Palette p = c.palettes[i];
                sb.Append("    { \"name\": ").Append(js.Serialize(p.name))
                  .Append(", \"colors\": ").Append(js.Serialize(p.colors)).Append(" }")
                  .Append(i < c.palettes.Count - 1 ? ",\r\n" : "\r\n");
            }
            sb.Append("  ]\r\n}\r\n");
            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
        }

        static Config Example()
        {
            Config c = new Config();
            c.active = Lang.T("Exemple", "Example");
            c.palettes = new List<Palette>();
            Palette p = new Palette();
            p.name = c.active;
            p.colors = new List<string> { "#ffffff", "#1e1e1e", "#f5c518" };
            c.palettes.Add(p);
            return c;
        }

        static void Clean(Config c)
        {
            if (c.palettes == null) c.palettes = new List<Palette>();
            c.palettes.RemoveAll(delegate(Palette p) { return p == null; });
            foreach (Palette p in c.palettes)
            {
                if (string.IsNullOrWhiteSpace(p.name)) p.name = Lang.T("Sans nom", "Untitled");
                List<string> clean = new List<string>();
                if (p.colors != null)
                    foreach (string s in p.colors)
                    {
                        string h = Hex.Normalize(s);
                        if (h != null && clean.Count < TrayApp.Slots) clean.Add(h);
                    }
                p.colors = clean;
            }
            Palette a = c.Active();
            c.active = a != null ? a.name : null;
        }
    }

    static class Hex
    {
        static readonly Regex Rx = new Regex("^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");

        // "#FFF", "fff", "#F5C518"… → "#f5c518" ; null si invalide.
        public static string Normalize(string s)
        {
            if (s == null) return null;
            Match m = Rx.Match(s.Trim());
            if (!m.Success) return null;
            string h = m.Groups[1].Value.ToLowerInvariant();
            if (h.Length == 3) h = new string(new char[] { h[0], h[0], h[1], h[1], h[2], h[2] });
            return "#" + h;
        }

        public static Color ToColor(string hex)
        {
            int v = Convert.ToInt32(hex.Substring(1), 16);
            return Color.FromArgb(255, (v >> 16) & 255, (v >> 8) & 255, v & 255);
        }

        public static string From(Color c)
        {
            return "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2");
        }

        public static bool IsLight(Color c)
        {
            return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255 > 0.6;
        }
    }

    // ---------- Icônes ----------

    static class Icons
    {
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        // Icône palette embarquée par build.bat (/resource:palette.ico), toutes tailles comprises.
        public static Icon App()
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("palette.ico"))
                return s != null ? new Icon(s) : null;
        }

        // La taille du fichier .ico la plus proche de celle demandée.
        public static Icon App(Size size)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("palette.ico"))
                return s != null ? new Icon(s, size) : null;
        }

        // Icône palette de la zone de notification.
        public static Icon Palette()
        {
            int s = TraySize();
            return App(new Size(s, s)) ?? Empty();
        }

        static int TraySize()
        {
            int s = SystemInformation.SmallIconSize.Width;
            return s < 16 ? 16 : s;
        }

        public static Icon Circle(Color color, bool check)
        {
            int s = TraySize();
            using (Bitmap bmp = new Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                bool light = Hex.IsLight(color);
                // Contour contrasté : un rond blanc reste visible sur barre claire, un noir sur barre sombre.
                float pw = Math.Max(1f, s / 16f);
                RectangleF r = new RectangleF(pw / 2 + 0.5f, pw / 2 + 0.5f, s - pw - 1f, s - pw - 1f);
                using (SolidBrush fill = new SolidBrush(color))
                    g.FillEllipse(fill, r);
                using (Pen outline = new Pen(light ? Color.FromArgb(150, 0, 0, 0) : Color.FromArgb(170, 255, 255, 255), pw))
                    g.DrawEllipse(outline, r);
                if (check)
                {
                    using (Pen tick = new Pen(light ? Color.Black : Color.White, Math.Max(2f, s / 8f)))
                    {
                        tick.StartCap = LineCap.Round;
                        tick.EndCap = LineCap.Round;
                        tick.LineJoin = LineJoin.Round;
                        g.DrawLines(tick, new PointF[] {
                            new PointF(s * 0.28f, s * 0.52f),
                            new PointF(s * 0.44f, s * 0.68f),
                            new PointF(s * 0.74f, s * 0.34f) });
                    }
                }
                return FromBitmap(bmp);
            }
        }

        // Rond pointillé gris : place réservée le temps que Windows range les icônes.
        public static Icon Empty()
        {
            int s = TraySize();
            using (Bitmap bmp = new Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float pw = Math.Max(1.5f, s / 12f);
                using (Pen p = new Pen(Color.FromArgb(220, 150, 150, 150), pw))
                {
                    p.DashStyle = DashStyle.Dash;
                    g.DrawEllipse(p, pw / 2 + 0.5f, pw / 2 + 0.5f, s - pw - 1f, s - pw - 1f);
                }
                return FromBitmap(bmp);
            }
        }

        static Icon FromBitmap(Bitmap bmp)
        {
            IntPtr h = bmp.GetHicon();
            try
            {
                using (Icon tmp = Icon.FromHandle(h))
                    return (Icon)tmp.Clone();
            }
            finally { DestroyIcon(h); }
        }
    }

    // ---------- Démarrage avec Windows ----------

    // Toujours activé à l'installation. Pour l'éviter : Paramètres → Applications → Démarrage.
    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "Palette-TaskBar";

        public static void Enable(string exe)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                k.SetValue(ValueName, "\"" + exe + "\"");
        }

        public static void Disable()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (k != null) k.DeleteValue(ValueName, false);
        }
    }

    // ---------- Échanges entre deux lancements ----------

    // Un second lancement demande à l'instance en cours d'ouvrir l'éditeur, ou de se fermer avant une mise à jour.
    static class Signals
    {
        public const string MutexName = "Palette-TaskBar_UneSeuleInstance";
        public const string OpenName = "Palette-TaskBar_Ouvrir";
        public const string QuitName = "Palette-TaskBar_Quitter";

        public static void Send(string name)
        {
            using (EventWaitHandle h = new EventWaitHandle(false, EventResetMode.AutoReset, name))
                h.Set();
        }
    }

    // ---------- Installation ----------

    // L'exe téléchargé s'installe lui-même pour l'utilisateur courant, sans droits administrateur :
    // copie dans %LOCALAPPDATA%\Programs, raccourci du menu Démarrer, lancement avec Windows,
    // et entrée dans Paramètres → Applications installées pour la désinstallation.
    static class Installer
    {
        const string Title = "Palette-TaskBar";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Palette-TaskBar";
        static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Palette-TaskBar");
        static readonly string InstalledExe = Path.Combine(InstallDir, "Palette-TaskBar.exe");
        static readonly string StartMenuLink = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Palette-TaskBar.lnk");

        public static bool IsInstalledCopy()
        {
            return string.Equals(Path.GetFullPath(Assembly.GetEntryAssembly().Location), Path.GetFullPath(InstalledExe),
                StringComparison.OrdinalIgnoreCase);
        }

        public static void Install()
        {
            bool update = File.Exists(InstalledExe);
            // Le choix de la langue vaut confirmation ; relancer l'installation permet d'en changer.
            bool english;
            if (!LanguageForm.Ask(out english)) return;

            try
            {
                Lang.Save(english);
                StopRunning();
                Directory.CreateDirectory(InstallDir);
                CopyWithRetry(Assembly.GetEntryAssembly().Location, InstalledExe);
                CreateShortcut(StartMenuLink, InstalledExe);
                Register();
                Startup.Enable(InstalledExe);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.T("L'installation a échoué :", "Installation failed:") + "\n\n" + ex.Message,
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Process.Start(InstalledExe);
            MessageBox.Show(update
                    ? Lang.T("Palette-TaskBar est à jour. Vos palettes sont conservées.",
                             "Palette-TaskBar is up to date. Your palettes are kept.")
                    : Lang.T("Palette-TaskBar est installé : vos couleurs sont à côté de l'horloge.\n\n" +
                             "Clic gauche pour copier une couleur.\nClic droit pour changer les paramètres.",
                             "Palette-TaskBar is installed: your colors are next to the clock.\n\n" +
                             "Left-click to copy a color.\nRight-click to change the settings."),
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void Uninstall()
        {
            DialogResult answer = MessageBox.Show(Lang.T(
                    "Supprimer aussi vos palettes ?\n\nOui : tout est effacé.\nNon : les palettes sont gardées pour une prochaine installation.",
                    "Also delete your palettes?\n\nYes: everything is erased.\nNo: your palettes are kept for a future installation."),
                Title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return;

            StopRunning();
            try { File.Delete(StartMenuLink); } catch (Exception) { }
            Startup.Disable();
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            if (answer == DialogResult.Yes)
            {
                Store.DeleteAll();
                Lang.Delete();
            }

            MessageBox.Show(Lang.T(
                    "Palette-TaskBar est désinstallé.\n\nSi l'icône est encore dans la barre des tâches : clic droit dessus → Désépingler.",
                    "Palette-TaskBar is uninstalled.\n\nIf its icon is still pinned to the taskbar: right-click it → Unpin from taskbar."),
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);

            // L'exe en cours ne peut pas s'effacer lui-même : cmd attend sa fermeture puis supprime le dossier.
            ProcessStartInfo cleanup = new ProcessStartInfo("cmd.exe",
                "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + InstallDir + "\"");
            cleanup.WorkingDirectory = Path.GetTempPath();
            cleanup.CreateNoWindow = true;
            cleanup.UseShellExecute = false;
            Process.Start(cleanup);
        }

        static void StopRunning()
        {
            int self = Process.GetCurrentProcess().Id;
            List<Process> others = new List<Process>();
            foreach (Process p in Process.GetProcessesByName("Palette-TaskBar"))
                if (p.Id != self) others.Add(p); else p.Dispose();
            if (others.Count == 0) return;

            Signals.Send(Signals.QuitName);
            foreach (Process p in others)
            {
                try
                {
                    if (!p.WaitForExit(5000)) { p.Kill(); p.WaitForExit(2000); }
                }
                catch (Exception) { }
                p.Dispose();
            }
        }

        static void CopyWithRetry(string from, string to)
        {
            for (int attempt = 1; ; attempt++)
            {
                try { File.Copy(from, to, true); return; }
                catch (IOException)
                {
                    if (attempt == 10) throw;
                    Thread.Sleep(300);
                }
            }
        }

        static void CreateShortcut(string link, string target)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            try
            {
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                Type t = shortcut.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(target) });
                t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { target + ",0" });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] {
                    Lang.T("Palettes de couleurs dans la barre des tâches", "Color palettes in the taskbar") });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                Marshal.ReleaseComObject(shortcut);
            }
            finally { Marshal.ReleaseComObject(shell); }
        }

        static void Register()
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "Palette-TaskBar");
                k.SetValue("DisplayIcon", InstalledExe + ",0");
                k.SetValue("DisplayVersion", Assembly.GetEntryAssembly().GetName().Version.ToString(3));
                k.SetValue("Publisher", "Cyril Neau");
                k.SetValue("InstallLocation", InstallDir);
                k.SetValue("UninstallString", "\"" + InstalledExe + "\" /uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
            }
        }
    }

    // ---------- Fenêtre d'installation : choix de la langue ----------

    // Écrite en anglais, puisque la langue n'est pas encore connue. Le choix vaut confirmation.
    class LanguageForm : Form
    {
        public bool English { get; private set; }

        public LanguageForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "Palette-TaskBar";
            Icon icon = Icons.App();
            if (icon != null) Icon = icon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(360, 116);

            PictureBox picture = new PictureBox();
            picture.SetBounds(18, 14, 40, 40);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            Icon big = Icons.App(new Size(64, 64));
            if (big != null) picture.Image = big.ToBitmap();
            Controls.Add(picture);

            AddLabel("Choose your language", 72, 22, new Font("Segoe UI", 11F, FontStyle.Bold), SystemColors.ControlText);

            Button french = AddButton("Français", 72, 72, 96);
            french.Click += delegate { Choose(false); };
            Button english = AddButton("English", 174, 72, 96);
            english.Click += delegate { Choose(true); };
            Button cancel = AddButton("Cancel", 276, 72, 72);
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;

            // Entrée = la langue de Windows.
            Button preferred = Lang.SystemIsFrench() ? french : english;
            AcceptButton = preferred;
            ActiveControl = preferred;

            ResumeLayout(false);
            PerformLayout();
        }

        public static bool Ask(out bool english)
        {
            using (LanguageForm f = new LanguageForm())
            {
                bool ok = f.ShowDialog() == DialogResult.OK;
                english = f.English;
                return ok;
            }
        }

        void Choose(bool english)
        {
            English = english;
            DialogResult = DialogResult.OK;
        }

        void AddLabel(string text, int x, int y, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            Controls.Add(l);
        }

        Button AddButton(string text, int x, int y, int w)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, 30);
            Controls.Add(b);
            return b;
        }
    }

    // ---------- Zone de notification ----------

    class TrayApp : ApplicationContext
    {
        public const int Slots = 6;

        [StructLayout(LayoutKind.Sequential)]
        struct NOTIFYICONIDENTIFIER { public uint cbSize; public IntPtr hWnd; public uint uID; public Guid guidItem; }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("shell32.dll")]
        static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

        // NotifyIcon ne publie ni sa fenêtre ni son identifiant, nécessaires pour demander sa position à Windows.
        static readonly FieldInfo IdField = typeof(NotifyIcon).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo WindowField = typeof(NotifyIcon).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);
        // Ouvre le menu comme un clic droit, pour le clic gauche sur l'icône palette.
        static readonly MethodInfo ShowMenuMethod = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.NonPublic | BindingFlags.Instance);

        readonly NotifyIcon[] tray = new NotifyIcon[Slots];
        // Icône palette : remplace les ronds quand on quitte, pour les faire revenir. Le programme ne se ferme jamais vraiment.
        readonly NotifyIcon home;
        readonly string[] slotHex = new string[Slots];
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        // Fenêtre invisible qui ramène sur le fil de l'interface les demandes reçues d'un autre lancement.
        readonly Control invoker = new Control();
        readonly System.Windows.Forms.Timer flashTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer startTimer = new System.Windows.Forms.Timer();
        // Une icône qui apparaît pour la première fois est rangée sous la flèche ^ : on la remonte dès qu'elle est inscrite.
        readonly System.Windows.Forms.Timer promoteTimer = new System.Windows.Forms.Timer();
        int promoteTries;
        // order[k] = icône affichée en k-ième position depuis la gauche.
        int[] order = { 0, 1, 2, 3, 4, 5 };
        bool measured;
        int flashSlot = -1;
        Config config;
        EditorForm editor;

        public TrayApp()
        {
            config = Store.Load();

            menu.Opening += delegate(object s, System.ComponentModel.CancelEventArgs e) { BuildMenu(); e.Cancel = false; };
            for (int i = 0; i < Slots; i++)
            {
                int slot = i;
                NotifyIcon ni = new NotifyIcon();
                ni.ContextMenuStrip = menu;
                ni.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) OnSlotClick(slot); };
                tray[i] = ni;
            }
            // Créée après les ronds : chaque icône garde son numéro d'un lancement à l'autre, et Windows sa place.
            home = new NotifyIcon();
            home.Icon = Icons.Palette();
            home.Text = "Palette-TaskBar";
            home.ContextMenuStrip = menu;
            home.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowMenu(); };

            IntPtr created = invoker.Handle;
            ListenForSignals();

            flashTimer.Interval = 900;
            flashTimer.Tick += delegate { EndFlash(); };

            // Laisse à Windows le temps de ranger les ronds à côté de l'horloge avant de lire leur place.
            startTimer.Interval = 2000;
            startTimer.Tick += delegate { OnStartTick(); };

            promoteTimer.Interval = 300;
            promoteTimer.Tick += delegate
            {
                if (PromoteIcons() || ++promoteTries >= 60) promoteTimer.Stop();
            };

            if (config.hidden) home.Visible = true;
            else ShowPlaceholders();
            StartPromoting();
            startTimer.Start();
        }

        // Windows mémorise la place de chaque icône. Les 6 ronds apparaissent d'abord en attente
        // (ajoutés de 6 vers 1 pour qu'une première installation soit dans l'ordre),
        // puis on lit leur position réelle à l'écran et les couleurs y sont rangées de gauche à droite.
        void ShowPlaceholders()
        {
            for (int i = Slots - 1; i >= 0; i--)
            {
                SetIcon(i, Icons.Empty());
                tray[i].Text = "Palette-TaskBar";
                tray[i].Visible = true;
            }
        }

        void OnStartTick()
        {
            startTimer.Stop();
            if (!config.hidden && !measured)
            {
                MeasureOrder();
                measured = true;
            }
            ApplyPalette();
        }

        // Met les ronds à jour. La première fois qu'ils s'affichent, Windows doit d'abord leur donner une place.
        void Refresh()
        {
            if (!config.hidden && !measured)
            {
                ShowPlaceholders();
                StartPromoting();
                startTimer.Stop();
                startTimer.Start();
                return;
            }
            if (!startTimer.Enabled) ApplyPalette();
        }

        void ListenForSignals()
        {
            EventWaitHandle open = new EventWaitHandle(false, EventResetMode.AutoReset, Signals.OpenName);
            EventWaitHandle quit = new EventWaitHandle(false, EventResetMode.AutoReset, Signals.QuitName);
            Thread listener = new Thread(delegate()
            {
                WaitHandle[] handles = { open, quit };
                while (true)
                {
                    if (WaitHandle.WaitAny(handles) == 0)
                        invoker.BeginInvoke(new MethodInvoker(OpenEditor));
                    else
                    {
                        invoker.BeginInvoke(new MethodInvoker(ExitThread));
                        return;
                    }
                }
            });
            listener.IsBackground = true;
            listener.Start();
        }

        void MeasureOrder()
        {
            int[] left = new int[Slots];
            for (int i = 0; i < Slots; i++)
                if (!TryGetLeft(tray[i], out left[i])) return;
            int[] sorted = { 0, 1, 2, 3, 4, 5 };
            Array.Sort(sorted, delegate(int a, int b) { return left[a] != left[b] ? left[a].CompareTo(left[b]) : a.CompareTo(b); });
            order = sorted;
        }

        static bool TryGetLeft(NotifyIcon ni, out int left)
        {
            left = 0;
            if (IdField == null || WindowField == null) return false;
            NativeWindow w = WindowField.GetValue(ni) as NativeWindow;
            if (w == null || w.Handle == IntPtr.Zero) return false;
            NOTIFYICONIDENTIFIER id = new NOTIFYICONIDENTIFIER();
            id.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONIDENTIFIER));
            id.hWnd = w.Handle;
            id.uID = (uint)(int)IdField.GetValue(ni);
            RECT r;
            if (Shell_NotifyIconGetRect(ref id, out r) != 0) return false;
            left = r.Left;
            return true;
        }

        void ApplyPalette()
        {
            flashTimer.Stop();
            flashSlot = -1;

            Palette p = config.Active();
            string paletteName = p != null ? p.name : "";
            List<string> colors = p != null && !config.hidden ? p.colors : new List<string>();
            // Ronds ou icône palette, jamais les deux ; l'icône seule aussi pour une palette vide, pour garder un accès au menu.
            bool showHome = colors.Count == 0;

            bool[] want = new bool[Slots];
            for (int k = 0; k < Slots; k++)
            {
                int t = order[k];
                slotHex[t] = k < colors.Count ? colors[k] : null;
                want[t] = slotHex[t] != null;
                if (want[t])
                {
                    SetIcon(t, Icons.Circle(Hex.ToColor(slotHex[t]), false));
                    tray[t].Text = Clip(slotHex[t] + "  ·  " + paletteName);
                }
            }

            // Masquer puis réafficher une icône connue ne change pas sa place.
            for (int i = 0; i < Slots; i++)
                if (tray[i].Visible != want[i]) tray[i].Visible = want[i];

            home.Text = Clip(config.hidden || p == null ? "Palette-TaskBar" : "Palette-TaskBar  ·  " + paletteName);
            if (showHome && !home.Visible)
            {
                home.Visible = true;
                StartPromoting();
            }
            else if (!showHome && home.Visible) home.Visible = false;
        }

        static string Clip(string text)
        {
            return text.Length > 63 ? text.Substring(0, 63) : text;
        }

        void SetIcon(int i, Icon icon)
        {
            Icon old = tray[i].Icon;
            tray[i].Icon = icon;
            if (old != null) old.Dispose();
        }

        void OnSlotClick(int slot)
        {
            if (startTimer.Enabled) return;
            if (slotHex[slot] == null) { OpenEditor(); return; }
            try { Clipboard.SetDataObject(slotHex[slot], true, 5, 100); }
            catch (Exception) { return; }

            if (flashSlot >= 0 && flashSlot != slot) EndFlash();
            flashSlot = slot;
            SetIcon(slot, Icons.Circle(Hex.ToColor(slotHex[slot]), true));
            flashTimer.Stop();
            flashTimer.Start();
        }

        void EndFlash()
        {
            flashTimer.Stop();
            if (flashSlot >= 0 && slotHex[flashSlot] != null)
                SetIcon(flashSlot, Icons.Circle(Hex.ToColor(slotHex[flashSlot]), false));
            flashSlot = -1;
        }

        void ShowMenu()
        {
            if (ShowMenuMethod != null)
            {
                try { ShowMenuMethod.Invoke(home, null); return; }
                catch (Exception) { }
            }
            OpenEditor();
        }

        void BuildMenu()
        {
            ToolStripItem[] olds = new ToolStripItem[menu.Items.Count];
            menu.Items.CopyTo(olds, 0);
            menu.Items.Clear();
            foreach (ToolStripItem old in olds) old.Dispose();

            Palette current = config.Active();
            foreach (Palette p in config.palettes)
            {
                string name = p.name;
                ToolStripMenuItem item = new ToolStripMenuItem(name.Replace("&", "&&"));
                item.Checked = !config.hidden && p == current;
                item.Click += delegate
                {
                    config.active = name;
                    config.hidden = false;
                    Store.Save(config);
                    Refresh();
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem manage = new ToolStripMenuItem(Lang.T("Gérer les palettes…", "Manage palettes…"));
            manage.Click += delegate { OpenEditor(); };
            menu.Items.Add(manage);

            if (config.hidden) return;
            // Quitter range les ronds et laisse l'icône palette, même après un redémarrage de Windows.
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem quit = new ToolStripMenuItem(Lang.T("Quitter", "Quit"));
            quit.Click += delegate
            {
                config.hidden = true;
                Store.Save(config);
                Refresh();
            };
            menu.Items.Add(quit);
        }

        void OpenEditor()
        {
            if (editor != null && !editor.IsDisposed)
            {
                if (editor.WindowState == FormWindowState.Minimized) editor.WindowState = FormWindowState.Normal;
                editor.Activate();
                return;
            }
            editor = new EditorForm(config, this);
            editor.Show();
            editor.Activate();
        }

        // « Enregistrer et afficher » : la palette choisie s'affiche, même si les couleurs étaient masquées.
        public void SavePalettes(List<Palette> palettes, string activeName)
        {
            config.palettes = palettes;
            config.active = activeName;
            config.hidden = false;
            Store.Save(config);
            Refresh();
        }

        void StartPromoting()
        {
            promoteTries = 0;
            promoteTimer.Stop();
            promoteTimer.Start();
        }

        // Windows 11 inscrit chaque nouvelle icône dans NotifyIconSettings peu après son apparition (0,3 s mesuré)
        // et la range sous la flèche ^ : on la remet à côté de l'horloge, l'effet est immédiat.
        // Vrai quand toutes les icônes affichées ont été trouvées.
        bool PromoteIcons()
        {
            List<int> waiting = new List<int>();
            if (IdField != null)
            {
                foreach (NotifyIcon ni in tray)
                    if (ni.Visible) waiting.Add((int)IdField.GetValue(ni));
                if (home.Visible) waiting.Add((int)IdField.GetValue(home));
            }
            bool any = false;
            try
            {
                string exe = Assembly.GetEntryAssembly().Location;
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings"))
                {
                    if (root == null) return true;  // pas de réglage par icône sur ce Windows
                    foreach (string sub in root.GetSubKeyNames())
                        using (RegistryKey k = root.OpenSubKey(sub, true))
                        {
                            if (k == null) continue;
                            string path = k.GetValue("ExecutablePath") as string;
                            if (path == null || !string.Equals(path, exe, StringComparison.OrdinalIgnoreCase)) continue;
                            any = true;
                            object uid = k.GetValue("UID");
                            if (uid is int) waiting.Remove((int)uid);
                            object v = k.GetValue("IsPromoted");
                            if (!(v is int) || (int)v != 1) k.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                        }
                }
            }
            catch (Exception) { return true; }
            return IdField != null ? waiting.Count == 0 : any;
        }

        protected override void ExitThreadCore()
        {
            foreach (NotifyIcon ni in tray)
            {
                ni.Visible = false;
                ni.Dispose();
            }
            home.Visible = false;
            home.Dispose();
            if (editor != null && !editor.IsDisposed) editor.Close();
            base.ExitThreadCore();
        }
    }

    // ---------- Fenêtre « Gérer les palettes » ----------

    class EditorForm : Form
    {
        class EditPal
        {
            public string Name;
            public string[] Slots = new string[TrayApp.Slots];
            public override string ToString() { return Name; }
        }

        static int[] customColors;

        readonly TrayApp app;
        readonly List<EditPal> pals = new List<EditPal>();
        readonly ListBox list = new ListBox();
        readonly TextBox nameBox = new TextBox();
        readonly Panel[] swatches = new Panel[TrayApp.Slots];
        readonly TextBox[] hexBoxes = new TextBox[TrayApp.Slots];
        readonly ToolTip tips = new ToolTip();
        readonly Font iconFont = LoadIconFont();
        bool loading, dirty;

        // Police d'icônes de Windows 11 ; absente (Windows 10), le bouton garde le mot « Pipette ».
        static Font LoadIconFont()
        {
            try { return new Font(new FontFamily("Segoe Fluent Icons"), 12F); }
            catch (ArgumentException) { return null; }
        }

        EditPal Current { get { return list.SelectedItem as EditPal; } }

        public EditorForm(Config config, TrayApp app)
        {
            this.app = app;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = Lang.T("Palettes de couleurs", "Color palettes");
            Icon icon = Icons.App();
            if (icon != null) Icon = icon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(500, 336);

            list.SetBounds(12, 12, 160, 252);
            list.IntegralHeight = false;
            list.SelectedIndexChanged += delegate { if (!loading) LoadSelected(); };
            Controls.Add(list);

            Button add = MakeButton(Lang.T("Nouvelle", "New"), 12, 272, 77, 28);
            add.Click += delegate { AddPalette(); };
            Button del = MakeButton(Lang.T("Supprimer", "Delete"), 95, 272, 77, 28);
            del.Click += delegate { DeletePalette(); };

            AddLabel(Lang.T("Nom", "Name"), 188, 15);
            nameBox.SetBounds(226, 12, 262, 23);
            nameBox.TextChanged += delegate { OnNameChanged(); };
            Controls.Add(nameBox);

            for (int i = 0; i < TrayApp.Slots; i++)
            {
                int slot = i;
                int y = 48 + i * 36;
                AddLabel((i + 1).ToString(), 190, y + 6);

                Panel sw = new Panel();
                sw.SetBounds(206, y, 30, 28);
                sw.BorderStyle = BorderStyle.FixedSingle;
                sw.Cursor = Cursors.Hand;
                sw.Click += delegate { Pick(slot); };
                sw.Paint += delegate(object s, PaintEventArgs e)
                {
                    if (Hex.Normalize(hexBoxes[slot].Text) != null) return;
                    using (Pen p = new Pen(Color.Silver))
                        e.Graphics.DrawLine(p, 0, sw.ClientSize.Height, sw.ClientSize.Width, 0);
                };
                swatches[i] = sw;
                Controls.Add(sw);

                TextBox hb = new TextBox();
                hb.SetBounds(244, y + 3, iconFont != null ? 140 : 96, 23);
                hb.MaxLength = 7;
                hb.TextChanged += delegate { OnHexChanged(slot); };
                hexBoxes[i] = hb;
                Controls.Add(hb);

                tips.SetToolTip(sw, Lang.T("Choisir une couleur", "Choose a color"));

                Button pipette = MakeButton(Lang.T("Pipette", "Eyedropper"), 392, y, 38, 28);
                if (iconFont != null)
                {
                    pipette.Font = iconFont;
                    pipette.Text = "";  // pipette de Windows 11
                }
                else pipette.SetBounds(348, y, 82, 28);
                pipette.Click += delegate { Eyedrop(slot); };
                tips.SetToolTip(pipette, Lang.T("Prendre une couleur à l'écran (Échap pour annuler)",
                                                "Pick a color on screen (Esc to cancel)"));
                Button clear = MakeButton(Lang.T("Vider", "Clear"), 438, y, 50, 28);
                clear.Click += delegate { hexBoxes[slot].Text = ""; };
            }

            Label hint = AddLabel(Lang.T("Un champ vide n'affiche pas de rond.", "An empty field shows no dot."), 188, 270);
            hint.ForeColor = SystemColors.GrayText;

            Button save = MakeButton(Lang.T("Enregistrer et afficher", "Save and show"), 254, 298, 154, 30);
            save.Click += delegate { Save(); };
            Button close = MakeButton(Lang.T("Fermer", "Close"), 414, 298, 74, 30);
            close.Click += delegate { Close(); };
            CancelButton = close;

            ResumeLayout(false);
            PerformLayout();

            // Copie de travail : rien n'est écrit tant qu'on n'enregistre pas.
            Palette active = config.Active();
            foreach (Palette p in config.palettes)
            {
                EditPal ep = new EditPal();
                ep.Name = p.name;
                for (int i = 0; i < p.colors.Count && i < TrayApp.Slots; i++) ep.Slots[i] = p.colors[i];
                pals.Add(ep);
                list.Items.Add(ep);
                if (p == active) list.SelectedItem = ep;
            }
            if (list.SelectedIndex < 0 && list.Items.Count > 0) list.SelectedIndex = 0;
            dirty = false;
        }

        Button MakeButton(string text, int x, int y, int w, int h)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            Controls.Add(b);
            return b;
        }

        Label AddLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            Controls.Add(l);
            return l;
        }

        void LoadSelected()
        {
            EditPal p = Current;
            loading = true;
            nameBox.Text = p != null ? p.Name : "";
            for (int i = 0; i < TrayApp.Slots; i++)
            {
                hexBoxes[i].Text = p != null && p.Slots[i] != null ? p.Slots[i] : "";
                UpdateSwatch(i);
            }
            loading = false;
        }

        void OnNameChanged()
        {
            EditPal p = Current;
            if (loading || p == null) return;
            p.Name = nameBox.Text;
            loading = true;
            list.Items[list.SelectedIndex] = p;
            loading = false;
            dirty = true;
        }

        void OnHexChanged(int slot)
        {
            UpdateSwatch(slot);
            EditPal p = Current;
            if (loading || p == null) return;
            p.Slots[slot] = hexBoxes[slot].Text.Trim();
            dirty = true;
        }

        void UpdateSwatch(int slot)
        {
            string text = hexBoxes[slot].Text.Trim();
            string hex = Hex.Normalize(text);
            swatches[slot].BackColor = hex != null ? Hex.ToColor(hex) : SystemColors.Control;
            hexBoxes[slot].BackColor = text.Length > 0 && hex == null ? Color.FromArgb(255, 222, 222) : SystemColors.Window;
            swatches[slot].Invalidate();
        }

        void Pick(int slot)
        {
            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.FullOpen = true;
                if (customColors != null) dlg.CustomColors = customColors;
                string hex = Hex.Normalize(hexBoxes[slot].Text);
                if (hex != null) dlg.Color = Hex.ToColor(hex);
                if (dlg.ShowDialog(this) == DialogResult.OK) hexBoxes[slot].Text = Hex.From(dlg.Color);
                customColors = dlg.CustomColors;
            }
        }

        void Eyedrop(int slot)
        {
            // La fenêtre devient transparente le temps de la capture, pour pouvoir viser ce qu'elle recouvre.
            Opacity = 0;
            Update();
            Thread.Sleep(150);
            try
            {
                using (PipetteForm f = new PipetteForm())
                    if (f.ShowDialog(this) == DialogResult.OK) hexBoxes[slot].Text = Hex.From(f.Picked);
            }
            finally
            {
                Opacity = 1;
                Activate();
            }
        }

        void AddPalette()
        {
            string baseName = Lang.T("Nouvelle palette", "New palette");
            string name = baseName;
            for (int n = 2; NameTaken(name, null); n++) name = baseName + " " + n;
            EditPal p = new EditPal();
            p.Name = name;
            pals.Add(p);
            list.Items.Add(p);
            list.SelectedItem = p;
            dirty = true;
            nameBox.Focus();
            nameBox.SelectAll();
        }

        void DeletePalette()
        {
            EditPal p = Current;
            if (p == null) return;
            if (pals.Count <= 1)
            {
                MessageBox.Show(this, Lang.T("Il faut garder au moins une palette.", "You need to keep at least one palette."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, Lang.T("Supprimer la palette « " + p.Name + " » ?", "Delete the palette “" + p.Name + "”?"),
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int index = list.SelectedIndex;
            pals.Remove(p);
            list.Items.Remove(p);
            list.SelectedIndex = Math.Min(index, list.Items.Count - 1);
            dirty = true;
        }

        bool NameTaken(string name, EditPal except)
        {
            foreach (EditPal p in pals)
                if (p != except && string.Equals(p.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        bool Save()
        {
            List<Palette> result = new List<Palette>();
            foreach (EditPal ep in pals)
            {
                string name = (ep.Name ?? "").Trim();
                if (name.Length == 0) return Refuse(ep, -1, Lang.T("Donnez un nom à cette palette.", "Give this palette a name."));
                if (NameTaken(name, ep))
                    return Refuse(ep, -1, Lang.T("Deux palettes s'appellent « " + name + " ».", "Two palettes are named “" + name + "”."));

                Palette p = new Palette();
                p.name = name;
                p.colors = new List<string>();
                for (int i = 0; i < TrayApp.Slots; i++)
                {
                    string raw = (ep.Slots[i] ?? "").Trim();
                    if (raw.Length == 0) continue;
                    string hex = Hex.Normalize(raw);
                    if (hex == null)
                        return Refuse(ep, i, Lang.T("« " + raw + " » n'est pas un code couleur valide (exemple : #f5c518).",
                                                    "“" + raw + "” is not a valid color code (example: #f5c518)."));
                    p.colors.Add(hex);
                }
                result.Add(p);
            }

            // Le formulaire reflète ce qui est enregistré : noms nettoyés, codes normalisés, trous retirés.
            for (int k = 0; k < pals.Count; k++)
            {
                pals[k].Name = result[k].name;
                for (int i = 0; i < TrayApp.Slots; i++)
                    pals[k].Slots[i] = i < result[k].colors.Count ? result[k].colors[i] : null;
            }
            int selected = Math.Max(0, list.SelectedIndex);
            loading = true;
            for (int k = 0; k < pals.Count; k++) list.Items[k] = pals[k];
            list.SelectedIndex = selected;
            loading = false;
            LoadSelected();

            app.SavePalettes(result, result[selected].name);
            dirty = false;
            return true;
        }

        bool Refuse(EditPal p, int slot, string message)
        {
            list.SelectedItem = p;
            if (slot >= 0) { hexBoxes[slot].Focus(); hexBoxes[slot].SelectAll(); }
            else { nameBox.Focus(); nameBox.SelectAll(); }
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (dirty && e.CloseReason == CloseReason.UserClosing)
            {
                DialogResult r = MessageBox.Show(this, Lang.T("Enregistrer les modifications ?", "Save your changes?"), Text,
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Save())) e.Cancel = true;
            }
            base.OnFormClosing(e);
        }
    }

    // ---------- Pipette ----------

    // Écran figé plein cadre avec une loupe qui suit la souris.
    // Clic gauche ou Entrée = prendre la couleur, Échap ou clic droit = annuler, flèches = déplacer d'un pixel.
    class PipetteForm : Form
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);

        const int SRCCOPY = 0x00CC0020;
        const int CAPTUREBLT = 0x40000000;  // inclut les fenêtres semi-transparentes
        const int Cells = 11;               // pixels visibles dans la loupe, impair pour avoir un pixel central

        readonly Bitmap shot;
        readonly Rectangle screen;
        readonly int zoom;
        readonly int infoHeight;
        readonly Font font = new Font("Segoe UI", 10F, FontStyle.Bold);
        Point at;
        Rectangle loupe;

        public Color Picked { get; private set; }

        public PipetteForm()
        {
            screen = SystemInformation.VirtualScreen;
            shot = new Bitmap(screen.Width, screen.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(shot))
            {
                IntPtr dest = g.GetHdc();
                IntPtr src = GetDC(IntPtr.Zero);
                BitBlt(dest, 0, 0, screen.Width, screen.Height, src, screen.Left, screen.Top, SRCCOPY | CAPTUREBLT);
                ReleaseDC(IntPtr.Zero, src);
                g.ReleaseHdc(dest);
            }
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                zoom = (int)Math.Round(10 * g.DpiX / 96f);
            infoHeight = 3 * zoom;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screen;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;

            Point c = Cursor.Position;
            MoveTo(new Point(c.X - screen.Left, c.Y - screen.Top));
        }

        Color Current()
        {
            return shot.GetPixel(at.X, at.Y);
        }

        void MoveTo(Point p)
        {
            at = new Point(Math.Max(0, Math.Min(shot.Width - 1, p.X)), Math.Max(0, Math.Min(shot.Height - 1, p.Y)));
            Rectangle next = LoupeBounds();
            Invalidate(Rectangle.Inflate(loupe, 3, 3));
            Invalidate(Rectangle.Inflate(next, 3, 3));
            loupe = next;
        }

        // En bas à droite du curseur, basculée de l'autre côté au bord de l'écran.
        Rectangle LoupeBounds()
        {
            int size = Cells * zoom;
            int height = size + infoHeight;
            Rectangle mon = Screen.FromPoint(new Point(at.X + screen.Left, at.Y + screen.Top)).Bounds;
            mon.Offset(-screen.Left, -screen.Top);
            int gap = 2 * zoom;
            int x = at.X + gap, y = at.Y + gap;
            if (x + size > mon.Right) x = at.X - gap - size;
            if (y + height > mon.Bottom) y = at.Y - gap - height;
            return new Rectangle(x, y, size, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImage(shot, e.ClipRectangle, e.ClipRectangle, GraphicsUnit.Pixel);
            if (!Rectangle.Inflate(loupe, 3, 3).IntersectsWith(e.ClipRectangle)) return;

            int size = Cells * zoom;
            Rectangle zoomed = new Rectangle(loupe.X, loupe.Y, size, size);
            g.FillRectangle(Brushes.Black, zoomed);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(shot, zoomed, new Rectangle(at.X - Cells / 2, at.Y - Cells / 2, Cells, Cells), GraphicsUnit.Pixel);
            g.PixelOffsetMode = PixelOffsetMode.Default;

            int cx = loupe.X + Cells / 2 * zoom, cy = loupe.Y + Cells / 2 * zoom;
            g.DrawRectangle(Pens.Black, cx - 1, cy - 1, zoom + 1, zoom + 1);
            g.DrawRectangle(Pens.White, cx, cy, zoom - 1, zoom - 1);

            Color color = Current();
            Rectangle info = new Rectangle(loupe.X, loupe.Y + size, size, infoHeight);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(32, 32, 32)))
                g.FillRectangle(bg, info);
            int pad = Math.Max(4, zoom / 2);
            Rectangle chip = new Rectangle(info.X + pad, info.Y + pad, infoHeight - 2 * pad, infoHeight - 2 * pad);
            using (SolidBrush fill = new SolidBrush(color))
                g.FillRectangle(fill, chip);
            g.DrawRectangle(Pens.Gray, chip);
            Rectangle text = new Rectangle(chip.Right + pad, info.Y, info.Right - chip.Right - pad, info.Height);
            TextRenderer.DrawText(g, Hex.From(color), font, text, Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

            g.DrawRectangle(Pens.Black, loupe.X - 2, loupe.Y - 2, loupe.Width + 3, loupe.Height + 3);
            g.DrawRectangle(Pens.White, loupe.X - 1, loupe.Y - 1, loupe.Width + 1, loupe.Height + 1);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            MoveTo(e.Location);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                MoveTo(e.Location);
                Picked = Current();
                DialogResult = DialogResult.OK;
            }
            else DialogResult = DialogResult.Cancel;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape: DialogResult = DialogResult.Cancel; return true;
                case Keys.Enter: Picked = Current(); DialogResult = DialogResult.OK; return true;
                case Keys.Left: Nudge(-1, 0); return true;
                case Keys.Right: Nudge(1, 0); return true;
                case Keys.Up: Nudge(0, -1); return true;
                case Keys.Down: Nudge(0, 1); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        static void Nudge(int dx, int dy)
        {
            Point p = Cursor.Position;
            Cursor.Position = new Point(p.X + dx, p.Y + dy);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                shot.Dispose();
                font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
