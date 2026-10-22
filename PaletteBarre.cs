// PaletteBarre : jusqu'à 6 ronds de couleur dans la zone de notification.
// Clic gauche = copie le code hex, clic droit = changer de palette / gérer les palettes.
// Compiler avec build.bat (compilateur C# fourni avec Windows, C# 5).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("PaletteBarre")]
[assembly: AssemblyProduct("PaletteBarre")]
[assembly: AssemblyDescription("Palettes de couleurs dans la barre des tâches")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace PaletteBarre
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            bool first;
            using (Mutex mutex = new Mutex(true, "PaletteBarre_UneSeuleInstance", out first))
            {
                if (!first) return;
                if (!IsWindows11())
                {
                    MessageBox.Show("PaletteBarre nécessite Windows 11.\nPaletteBarre requires Windows 11.",
                        "PaletteBarre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { SetProcessDPIAware(); } catch (Exception) { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
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

    // ---------- Données ----------

    public class Palette
    {
        public string name { get; set; }
        public List<string> colors { get; set; }
    }

    public class Config
    {
        public string active { get; set; }
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
        static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PaletteBarre");
        public static readonly string FilePath = Path.Combine(Folder, "palettes.json");
        // Les premières versions rangeaient le fichier à côté de l'exe : il est repris une fois.
        static readonly string OldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "palettes.json");

        public static Config Load()
        {
            Directory.CreateDirectory(Folder);
            if (!File.Exists(FilePath) && File.Exists(OldPath))
            {
                File.Copy(OldPath, FilePath);
                try { File.Delete(OldPath); } catch (Exception) { }
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
                    MessageBox.Show("palettes.json est illisible, la palette d'exemple est utilisée en attendant.\n\n" + ex.Message,
                        "PaletteBarre", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            sb.Append("{\r\n  \"active\": ").Append(js.Serialize(c.active)).Append(",\r\n  \"palettes\": [\r\n");
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
            c.active = "Exemple";
            c.palettes = new List<Palette>();
            Palette p = new Palette();
            p.name = "Exemple";
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
                if (string.IsNullOrWhiteSpace(p.name)) p.name = "Sans nom";
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

        static int Size()
        {
            int s = SystemInformation.SmallIconSize.Width;
            return s < 16 ? 16 : s;
        }

        public static Icon Circle(Color color, bool check)
        {
            int s = Size();
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

        // Rond pointillé gris : palette vide, garde un point d'accès au menu.
        public static Icon Empty()
        {
            int s = Size();
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

    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "PaletteBarre";

        public static bool IsEnabled()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(ValueName) != null;
        }

        public static void Set(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(ValueName, "\"" + Assembly.GetEntryAssembly().Location + "\"");
                else k.DeleteValue(ValueName, false);
            }
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

        readonly NotifyIcon[] tray = new NotifyIcon[Slots];
        readonly string[] slotHex = new string[Slots];
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly System.Windows.Forms.Timer flashTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer startTimer = new System.Windows.Forms.Timer();
        // order[k] = icône affichée en k-ième position depuis la gauche.
        int[] order = { 0, 1, 2, 3, 4, 5 };
        int startStep;
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
                ni.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) OnSlotClick(slot); };                tray[i] = ni;
            }

            flashTimer.Interval = 900;
            flashTimer.Tick += delegate { EndFlash(); };

            // Windows mémorise la place de chaque icône et met toute nouvelle icône à gauche.
            // Au lancement les 6 apparaissent (ajoutées de 6 vers 1 pour qu'une première installation soit dans l'ordre),
            // puis on lit leur position réelle à l'écran et les couleurs y sont rangées de gauche à droite.
            for (int i = Slots - 1; i >= 0; i--)
            {
                SetIcon(i, Icons.Empty());
                tray[i].Text = "PaletteBarre";
                tray[i].Visible = true;
            }
            startTimer.Interval = 1000;
            startTimer.Tick += delegate
            {
                startStep++;
                if (startStep == 1) { PromoteIcons(); return; }
                startTimer.Stop();
                MeasureOrder();
                ApplyPalette();
            };
            startTimer.Start();
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

        public void ApplyPalette()
        {
            flashTimer.Stop();
            flashSlot = -1;

            Palette p = config.Active();
            List<string> colors = p != null ? p.colors : new List<string>();
            string paletteName = p != null ? p.name : "";

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
            if (colors.Count == 0)
            {
                int t = order[0];
                want[t] = true;
                SetIcon(t, Icons.Empty());
                tray[t].Text = Clip("Palette « " + paletteName + " » vide : clic pour la remplir");
            }

            // Masquer puis réafficher une icône connue ne change pas sa place.
            for (int i = 0; i < Slots; i++)
                if (tray[i].Visible != want[i]) tray[i].Visible = want[i];
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
                item.Checked = p == current;
                item.Click += delegate
                {
                    config.active = name;
                    Store.Save(config);
                    ApplyPalette();
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem manage = new ToolStripMenuItem("Gérer les palettes…");
            manage.Click += delegate { OpenEditor(); };
            menu.Items.Add(manage);

            ToolStripMenuItem startup = new ToolStripMenuItem("Lancer au démarrage de Windows");
            startup.Checked = Startup.IsEnabled();
            startup.Click += delegate { Startup.Set(!Startup.IsEnabled()); };
            menu.Items.Add(startup);

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem quit = new ToolStripMenuItem("Quitter");
            quit.Click += delegate { ExitThread(); };
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

        public void SavePalettes(List<Palette> palettes, string activeName)
        {
            config.palettes = palettes;
            config.active = activeName;
            Store.Save(config);
            ApplyPalette();
        }

        // Windows 11 range les nouvelles icônes sous la flèche ^ : on les épingle dans la barre.
        static void PromoteIcons()
        {
            try
            {
                string exe = Assembly.GetEntryAssembly().Location;
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings"))
                {
                    if (root == null) return;
                    foreach (string sub in root.GetSubKeyNames())
                        using (RegistryKey k = root.OpenSubKey(sub, true))
                        {
                            if (k == null) continue;
                            string path = k.GetValue("ExecutablePath") as string;
                            if (path == null || !string.Equals(path, exe, StringComparison.OrdinalIgnoreCase)) continue;
                            object v = k.GetValue("IsPromoted");
                            if (!(v is int) || (int)v != 1) k.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                        }
                }
            }
            catch (Exception) { }
        }

        protected override void ExitThreadCore()
        {
            foreach (NotifyIcon ni in tray)
            {
                ni.Visible = false;
                ni.Dispose();
            }
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
            Text = "Palettes de couleurs";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(500, 336);

            list.SetBounds(12, 12, 160, 252);
            list.IntegralHeight = false;
            list.SelectedIndexChanged += delegate { if (!loading) LoadSelected(); };
            Controls.Add(list);

            Button add = MakeButton("Nouvelle", 12, 272, 77, 28);
            add.Click += delegate { AddPalette(); };
            Button del = MakeButton("Supprimer", 95, 272, 77, 28);
            del.Click += delegate { DeletePalette(); };

            AddLabel("Nom", 188, 15);
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

                tips.SetToolTip(sw, "Choisir une couleur");

                Button pipette = MakeButton("Pipette", 392, y, 38, 28);
                if (iconFont != null)
                {
                    pipette.Font = iconFont;
                    pipette.Text = "";  // pipette de Windows 11
                }
                else pipette.SetBounds(348, y, 82, 28);
                pipette.Click += delegate { Eyedrop(slot); };
                tips.SetToolTip(pipette, "Prendre une couleur à l'écran (Échap pour annuler)");
                Button clear = MakeButton("Vider", 438, y, 50, 28);
                clear.Click += delegate { hexBoxes[slot].Text = ""; };
            }

            Label hint = AddLabel("Un champ vide n'affiche pas de rond.", 188, 270);
            hint.ForeColor = SystemColors.GrayText;

            Button save = MakeButton("Enregistrer et afficher", 254, 298, 154, 30);
            save.Click += delegate { Save(); };
            Button close = MakeButton("Fermer", 414, 298, 74, 30);
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
            string name = "Nouvelle palette";
            for (int n = 2; NameTaken(name, null); n++) name = "Nouvelle palette " + n;
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
                MessageBox.Show(this, "Il faut garder au moins une palette.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "Supprimer la palette « " + p.Name + " » ?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
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
                if (name.Length == 0) return Refuse(ep, -1, "Donne un nom à cette palette.");
                if (NameTaken(name, ep)) return Refuse(ep, -1, "Deux palettes s'appellent « " + name + " ».");

                Palette p = new Palette();
                p.name = name;
                p.colors = new List<string>();
                for (int i = 0; i < TrayApp.Slots; i++)
                {
                    string raw = (ep.Slots[i] ?? "").Trim();
                    if (raw.Length == 0) continue;
                    string hex = Hex.Normalize(raw);
                    if (hex == null) return Refuse(ep, i, "« " + raw + " » n'est pas un code couleur valide (exemple : #f5c518).");
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
                DialogResult r = MessageBox.Show(this, "Enregistrer les modifications ?", Text,
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
