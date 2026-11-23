using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NinjaSagaPvP.Launcher
{
    internal static class Program
    {
        internal static string[] Args;

        [STAThread]
        private static void Main(string[] args)
        {
            Args = args;
            bool created;
            using (var mutex = new Mutex(true, "NinjaSagaPvPLauncher", out created))
            {
                if (!created)
                {
                    MessageBox.Show("El launcher ya está abierto.", "Ninja Saga PvP");
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    public class Manifest
    {
        public string version { get; set; }
        public string zip_url { get; set; }
        public long zip_size { get; set; }
        public string zip_sha256 { get; set; }
        public string exe { get; set; }
        public string notes { get; set; }
    }

    public class Config
    {
        public string install_dir { get; set; }
        public string installed_version { get; set; }
    }

    internal sealed class JadeBar : Control
    {
        private double _value;

        public JadeBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 18;
            BackColor = Color.FromArgb(10, 16, 13);
        }

        public double Value
        {
            get { return _value; }
            set
            {
                if (value < 0) value = 0;
                if (value > 1) value = 1;
                _value = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Rounded(r, 7))
            {
                using (var bg = new SolidBrush(Color.FromArgb(18, 28, 24))) e.Graphics.FillPath(bg, path);
                int w = (int)Math.Round((Width - 2) * _value);
                if (w > 1)
                {
                    var fr = new Rectangle(1, 1, w, Height - 2);
                    using (var fg = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(w, 2), Height), Color.FromArgb(60, 255, 190), Color.FromArgb(10, 143, 102), 0f))
                        e.Graphics.FillRectangle(fg, fr);
                }
                using (var pen = new Pen(Color.FromArgb(38, 66, 55))) e.Graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class MainForm : Form
    {
        private const string DefaultManifestUrl = "https://nso.173-249-31-27.sslip.io/downloads/launcher.json";
        private const string LauncherVersion = "1.0.6";
        private const string DefaultExe = "Ninja Saga Online - Private Server Beta.exe";

        private readonly string _dataDir;
        private readonly string _configPath;
        private readonly string _manifestUrl;
        private readonly bool _auto;
        private readonly bool _noLaunch;

        private Manifest _manifest;
        private Config _config;
        private string _installDir;
        private bool _busy;
        private bool _autoStarted;

        private PictureBox _logo;
        private Image _bg;
        private Label _title;
        private Label _subtitle;
        private Label _version;
        private Label _status;
        private Label _detail;
        private Label _footer;
        private JadeBar _bar;
        private Button _primary;
        private Button _folder;
        private Button _open;
        private TextBox _log;

        public MainForm()
        {
            _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NinjaSagaPvP");
            Directory.CreateDirectory(_dataDir);
            _configPath = Path.Combine(_dataDir, "config.json");
            _manifestUrl = DefaultManifestUrl;

            string dirArg = GetArg("--dir=");
            if (!string.IsNullOrEmpty(dirArg)) _installDir = dirArg;
            string manifestArg = GetArg("--manifest=");
            if (!string.IsNullOrEmpty(manifestArg)) _manifestUrl = manifestArg;
            _auto = HasArg("--auto");
            _noLaunch = HasArg("--no-launch");

            LoadConfig();
            if (string.IsNullOrEmpty(_installDir)) _installDir = _config.install_dir;
            if (string.IsNullOrEmpty(_installDir)) _installDir = DefaultInstallDir();

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            BuildUi();
        }

        private static bool HasArg(string value)
        {
            foreach (var a in Program.Args) if (string.Equals(a, value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetArg(string prefix)
        {
            foreach (var a in Program.Args)
                if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return a.Substring(prefix.Length).Trim('"');
            return null;
        }

        private string DefaultInstallDir()
        {
            try
            {
                var d = new DriveInfo("D:\\");
                if (d.IsReady && d.AvailableFreeSpace > 8L * 1024 * 1024 * 1024) return "D:\\NinjaSagaPvP";
            }
            catch { }
            return Path.Combine(_dataDir, "game");
        }

        private string ExePath
        {
            get
            {
                string exe = _manifest != null && !string.IsNullOrEmpty(_manifest.exe) ? _manifest.exe : DefaultExe;
                return Path.Combine(_installDir, exe);
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var ser = new JavaScriptSerializer();
                    _config = ser.Deserialize<Config>(File.ReadAllText(_configPath));
                }
            }
            catch (Exception ex) { Log("Config: " + ex.Message); }
            if (_config == null) _config = new Config();
        }

        private void SaveConfig()
        {
            try
            {
                _config.install_dir = _installDir;
                var ser = new JavaScriptSerializer();
                File.WriteAllText(_configPath, ser.Serialize(_config));
            }
            catch (Exception ex) { Log("Guardar config: " + ex.Message); }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_bg == null)
            {
                base.OnPaintBackground(e);
                return;
            }
            float scale = Math.Max((float)ClientSize.Width / _bg.Width, (float)ClientSize.Height / _bg.Height);
            int w = (int)(_bg.Width * scale);
            int h = (int)(_bg.Height * scale);
            int x = (ClientSize.Width - w) / 2;
            int y = (ClientSize.Height - h) / 2;
            e.Graphics.DrawImage(_bg, new Rectangle(x, y, w, h));
            using (var overlay = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
            {
                e.Graphics.FillRectangle(overlay, ClientRectangle);
            }
        }

        private void BuildUi()
        {
            Text = "Ninja Saga PvP — Launcher";
            ClientSize = new Size(800, 500);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(8, 12, 10);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("loading.jpg"))
                {
                    if (s != null) _bg = Image.FromStream(s);
                }
            }
            catch { }

            var topLine = new Panel();
            topLine.BackColor = Color.FromArgb(228, 20, 3);
            topLine.SetBounds(0, 0, 800, 3);
            Controls.Add(topLine);

            _logo = new PictureBox();
            _logo.SetBounds(26, 22, 96, 96);
            _logo.SizeMode = PictureBoxSizeMode.Zoom;
            _logo.BackColor = Color.Transparent;
            Controls.Add(_logo);

            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("profile.png"))
                {
                    if (s != null)
                    {
                        var bmp = new Bitmap(s);
                        _logo.Image = bmp;
                        try { Icon = Icon.FromHandle(bmp.GetHicon()); } catch { }
                    }
                }
            }
            catch { }

            _title = new Label();
            _title.Text = "NINJA SAGA PvP";
            _title.Font = new Font("Segoe UI", 25f, FontStyle.Bold);
            _title.ForeColor = Color.FromArgb(245, 245, 245);
            _title.AutoSize = true;
            _title.SetBounds(138, 20, 0, 0);
            Controls.Add(_title);

            _subtitle = new Label();
            _subtitle.Text = "LAUNCHER · TEMPORADA 1";
            _subtitle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _subtitle.ForeColor = Color.FromArgb(220, 210, 210);
            _subtitle.AutoSize = true;
            _subtitle.SetBounds(142, 66, 0, 0);
            Controls.Add(_subtitle);

            _version = new Label();
            _version.Text = "v" + LauncherVersion;
            _version.Font = new Font("Segoe UI", 9f);
            _version.ForeColor = Color.FromArgb(210, 200, 200);
            _version.TextAlign = ContentAlignment.TopRight;
            _version.SetBounds(650, 28, 124, 20);
            Controls.Add(_version);

            _status = new Label();
            _status.Text = "Comprobando versión...";
            _status.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _status.ForeColor = Color.White;
            _status.AutoEllipsis = true;
            _status.SetBounds(26, 140, 748, 26);
            Controls.Add(_status);

            _detail = new Label();
            _detail.Text = "";
            _detail.Font = new Font("Segoe UI", 9f);
            _detail.ForeColor = Color.FromArgb(220, 215, 215);
            _detail.SetBounds(26, 168, 748, 42);
            Controls.Add(_detail);

            _bar = new JadeBar();
            _bar.SetBounds(26, 218, 748, 18);
            Controls.Add(_bar);

            _primary = MakeButton("COMPROBANDO...", Color.FromArgb(25, 224, 160), Color.FromArgb(5, 10, 8), true);
            _primary.SetBounds(26, 258, 380, 52);
            _primary.Enabled = false;
            _primary.Click += Primary_Click;
            Controls.Add(_primary);

            _folder = MakeButton("CAMBIAR CARPETA", Color.FromArgb(18, 28, 24), Color.White, false);
            _folder.SetBounds(420, 258, 170, 52);
            _folder.Click += Folder_Click;
            Controls.Add(_folder);

            _open = MakeButton("ABRIR CARPETA", Color.FromArgb(18, 28, 24), Color.White, false);
            _open.SetBounds(604, 258, 170, 52);
            _open.Click += Open_Click;
            Controls.Add(_open);

            _log = new TextBox();
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.FromArgb(11, 18, 15);
            _log.ForeColor = Color.FromArgb(143, 163, 154);
            _log.BorderStyle = BorderStyle.FixedSingle;
            _log.Font = new Font("Consolas", 8.25f);
            _log.SetBounds(26, 330, 748, 110);
            Controls.Add(_log);

            _footer = new Label();
            _footer.Text = "Servidor: nso.173-249-31-27.sslip.io · PvP: pvp.173-249-31-27.sslip.io";
            _footer.Font = new Font("Segoe UI", 8f);
            _footer.ForeColor = Color.FromArgb(100, 118, 110);
            _footer.SetBounds(26, 452, 748, 20);
            Controls.Add(_footer);

            Shown += OnShown;
        }

        private Button MakeButton(string text, Color back, Color fore, bool primary)
        {
            var b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(42, 70, 59);
            b.BackColor = back;
            b.ForeColor = fore;
            b.Font = new Font("Segoe UI", primary ? 12f : 9f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.MouseEnter += delegate
            {
                if (!b.Enabled) return;
                b.BackColor = primary ? Color.FromArgb(70, 255, 200) : Color.FromArgb(26, 40, 34);
            };
            b.MouseLeave += delegate
            {
                b.BackColor = primary ? Color.FromArgb(25, 224, 160) : Color.FromArgb(18, 28, 24);
            };
            return b;
        }

        private async void OnShown(object sender, EventArgs e)
        {
            await InitAsync();
            if (_auto && !_autoStarted)
            {
                _autoStarted = true;
                if (NeedsInstall()) await InstallAsync();
                Log("Modo automático: cerrando launcher.");
                Application.Exit();
            }
        }

        private async Task InitAsync()
        {
            _busy = true;
            SetBusy(true, "Comprobando versión...");
            Log("Launcher " + LauncherVersion + " iniciado.");
            Log("Manifest: " + _manifestUrl);
            try
            {
                _manifest = await FetchManifestAsync();
                Log("Versión disponible: " + _manifest.version + " (" + FormatGb(_manifest.zip_size) + ")");
                if (!string.IsNullOrEmpty(_manifest.notes)) Log(_manifest.notes);
            }
            catch (Exception ex)
            {
                _manifest = null;
                Log("No se pudo leer el manifest: " + ex.Message);
            }

            if (File.Exists(ExePath) && string.IsNullOrEmpty(_config.installed_version) && _manifest != null)
            {
                _config.installed_version = _manifest.version;
                SaveConfig();
            }

            _busy = false;
            SetBusy(false, null);
            UpdateUi();
        }

        private async Task<Manifest> FetchManifestAsync()
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(25);
                string url = _manifestUrl + (_manifestUrl.IndexOf('?') >= 0 ? "&" : "?") + "t=" + DateTime.UtcNow.Ticks;
                string json = await client.GetStringAsync(url);
                var ser = new JavaScriptSerializer();
                var m = ser.Deserialize<Manifest>(json);
                if (m == null || string.IsNullOrEmpty(m.version) || string.IsNullOrEmpty(m.zip_url) || m.zip_size <= 0 || string.IsNullOrEmpty(m.zip_sha256))
                    throw new Exception("El manifest está incompleto.");
                return m;
            }
        }

        private bool IsInstalled()
        {
            return File.Exists(ExePath);
        }

        private bool NeedsInstall()
        {
            if (!IsInstalled()) return true;
            if (_manifest == null) return false;
            return !string.Equals(_config.installed_version, _manifest.version, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateUi()
        {
            _version.Text = "v" + LauncherVersion;
            _footer.Text = "Servidor: nso.173-249-31-27.sslip.io · PvP: pvp.173-249-31-27.sslip.io";

            if (_busy)
            {
                _primary.Enabled = false;
                _folder.Enabled = false;
                _open.Enabled = false;
                return;
            }

            _folder.Enabled = true;
            _open.Enabled = true;

            if (!IsInstalled())
            {
                if (_manifest != null)
                {
                    _primary.Text = "INSTALAR";
                    _primary.Enabled = true;
                    _status.Text = "Listo para instalar";
                    _detail.Text = string.Format("El launcher descargará el juego completo ({0}) y lo instalará en {1}.", FormatGb(_manifest.zip_size), _installDir);
                }
                else
                {
                    _primary.Text = "REINTENTAR";
                    _primary.Enabled = true;
                    _status.Text = "Sin conexión";
                    _detail.Text = "No se pudo conectar con el servidor. Revisá tu conexión y reintentá.";
                }
                return;
            }

            if (_manifest != null && !string.Equals(_config.installed_version, _manifest.version, StringComparison.OrdinalIgnoreCase))
            {
                _primary.Text = "ACTUALIZAR";
                _primary.Enabled = true;
                _status.Text = "Actualización disponible";
                _detail.Text = string.Format("Hay una versión nueva ({0}). Se descargará y se instalará sobre la actual.", _manifest.version);
                return;
            }

            _primary.Text = "JUGAR";
            _primary.Enabled = true;
            _status.Text = "Listo para jugar";
            _detail.Text = "Instalado en " + _installDir;
        }

        private async void Primary_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            if (!IsInstalled())
            {
                if (_manifest == null) await InitAsync();
                else await InstallAsync();
                return;
            }
            if (_manifest != null && !string.Equals(_config.installed_version, _manifest.version, StringComparison.OrdinalIgnoreCase))
            {
                await InstallAsync();
                return;
            }
            LaunchGame();
        }

        private async Task InstallAsync()
        {
            if (_busy || _manifest == null) return;
            _busy = true;
            SetBusy(true, "Preparando instalación...");
            bool ok = false;

            try
            {
                Directory.CreateDirectory(_installDir);
                string part = Path.Combine(_installDir, ".download", "client.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(part));

                long required = _manifest.zip_size + 4700000000L;
                var drive = new DriveInfo(Path.GetPathRoot(_installDir));
                if (drive.AvailableFreeSpace < required)
                    throw new Exception(string.Format("Espacio insuficiente en {0}. Necesitás {1:N1} GB libres.", drive.Name, required / 1073741824.0));

                IProgress<double> progress = new Progress<double>(delegate(double v) { _bar.Value = v; });
                IProgress<string> status = new Progress<string>(delegate(string s) { _status.Text = s; });

                bool verified = false;
                for (int attempt = 1; attempt <= 2 && !verified; attempt++)
                {
                    if (File.Exists(part) && new FileInfo(part).Length == _manifest.zip_size)
                    {
                        status.Report("Verificando archivos...");
                        string h0 = await Task.Run(delegate { return Sha256File(part, progress); });
                        if (string.Equals(h0, _manifest.zip_sha256, StringComparison.OrdinalIgnoreCase)) { verified = true; break; }
                        File.Delete(part);
                    }

                    await DownloadAsync(_manifest, part, progress, status);
                    status.Report("Verificando archivos...");
                    string hash = await Task.Run(delegate { return Sha256File(part, progress); });
                    if (string.Equals(hash, _manifest.zip_sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        verified = true;
                    }
                    else
                    {
                        Log("El hash no coincide; se descarta y se reintenta.");
                        try { File.Delete(part); } catch { }
                    }
                }

                if (!verified) throw new Exception("No se pudo verificar el archivo descargado.");

                await ExtractAsync(part, _installDir, progress, status);

                if (!File.Exists(ExePath)) throw new Exception("No se encontró el ejecutable del juego después de extraer.");

                _config.install_dir = _installDir;
                _config.installed_version = _manifest.version;
                SaveConfig();
                EnsureShortcut();

                try { Directory.Delete(Path.Combine(_installDir, ".download"), true); } catch { }

                ok = true;
                _status.Text = "Instalación completa";
                _bar.Value = 1;
                Log("Instalación completa en " + _installDir);
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                _status.Text = "Error";
                MessageBox.Show(this, ex.Message, "Ninja Saga PvP", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _busy = false;
                SetBusy(false, null);
                UpdateUi();
            }

            if (ok && !_noLaunch && !_auto) LaunchGame();
        }

        private async Task DownloadAsync(Manifest m, string part, IProgress<double> progress, IProgress<string> status)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(part));
            long existing = File.Exists(part) ? new FileInfo(part).Length : 0;
            if (existing >= m.zip_size) existing = 0;

            using (var client = new HttpClient())
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                var req = new HttpRequestMessage(HttpMethod.Get, m.zip_url);
                if (existing > 0) req.Headers.Range = new RangeHeaderValue(existing, null);

                using (var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (existing > 0 && resp.StatusCode != HttpStatusCode.PartialContent)
                    {
                        if (File.Exists(part)) File.Delete(part);
                        await DownloadAsync(m, part, progress, status);
                        return;
                    }

                    resp.EnsureSuccessStatusCode();
                    long total = m.zip_size;
                    long start = existing;

                    using (var fs = new FileStream(part, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 131072, true))
                    using (var input = await resp.Content.ReadAsStreamAsync())
                    {
                        var buffer = new byte[131072];
                        long done = start;
                        var sw = Stopwatch.StartNew();
                        long last = 0;
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, read);
                            done += read;
                            if (sw.ElapsedMilliseconds - last >= 400)
                            {
                                last = sw.ElapsedMilliseconds;
                                progress.Report((double)done / total);
                                double mb = done / 1048576.0;
                                double speed = (done - start) / 1048576.0 / Math.Max(sw.Elapsed.TotalSeconds, 0.1);
                                status.Report(string.Format("Descargando {0:N0} / {1:N0} MB ({2:N1}%) · {3:N1} MB/s", mb, total / 1048576.0, done * 100.0 / total, speed));
                            }
                        }
                    }
                    progress.Report(1);
                }
            }
        }

        private static string Sha256File(string path, IProgress<double> progress)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576))
            {
                var buf = new byte[1048576];
                long total = fs.Length;
                long done = 0;
                int read;
                while ((read = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    sha.TransformBlock(buf, 0, read, null, 0);
                    done += read;
                    if (progress != null) progress.Report((double)done / total);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                var sb = new StringBuilder();
                foreach (var b in sha.Hash) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        private async Task ExtractAsync(string zip, string dir, IProgress<double> progress, IProgress<string> status)
        {
            string sevenZip = EnsureSevenZip();
            status.Report("Extrayendo archivos...");
            Log("Extrayendo con 7-Zip...");

            var psi = new ProcessStartInfo();
            psi.FileName = sevenZip;
            psi.Arguments = string.Format("x -y -bsp1 -o\"{0}\" \"{1}\"", dir, zip);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            using (var p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    var match = Regex.Match(e.Data, @"(\d{1,3})%");
                    if (match.Success)
                    {
                        int v;
                        if (int.TryParse(match.Groups[1].Value, out v)) progress.Report(v / 100.0);
                    }
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null) Log("7z: " + e.Data);
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                await Task.Run(delegate { p.WaitForExit(); });
                if (p.ExitCode != 0) throw new Exception("7-Zip terminó con código " + p.ExitCode + ".");
            }
        }

        private string EnsureSevenZip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "NinjaSagaPvP", "tools");
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "7z.exe");
            string dll = Path.Combine(dir, "7z.dll");
            ExtractResource("sevenzip.exe", exe);
            ExtractResource("sevenzip.dll", dll);
            return exe;
        }

        private static void ExtractResource(string name, string path)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 1000) return;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) throw new Exception("Falta el recurso " + name + ".");
                using (var f = File.Create(path)) s.CopyTo(f);
            }
        }

        private void LaunchGame()
        {
            if (!File.Exists(ExePath))
            {
                MessageBox.Show(this, "No se encontró el juego. Reinstalalo con el launcher.", "Ninja Saga PvP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                Log("Abriendo " + ExePath);
                PatchAirSplash();
                var psi = new ProcessStartInfo();
                psi.FileName = ExePath;
                psi.WorkingDirectory = _installDir;
                psi.UseShellExecute = true;
                Process.Start(psi);
                Close();
            }
            catch (Exception ex)
            {
                Log("ERROR al abrir: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Ninja Saga PvP", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// El runtime AIR "free tier" crea una ventana AIR_Splash de 600x350 con
        /// el logo de HARMAN/Samsung. La ventana debe seguir mostrándose para que
        /// el runtime continúe, pero la dejamos en 1x1 para que no se vea.
        /// </summary>
        private void PatchAirSplash()
        {
            try
            {
                string dll = Path.Combine(_installDir, "Adobe AIR", "Versions", "1.0", "Adobe AIR.dll");
                if (!File.Exists(dll)) return;

                byte[] data = File.ReadAllBytes(dll);
                byte[] oldPat = { 0x6A, 0x14, 0x68, 0x5E, 0x01, 0x00, 0x00, 0x68, 0x58, 0x02, 0x00, 0x00, 0xFF, 0xB6 };
                byte[] newPat = { 0x6A, 0x14, 0x68, 0x01, 0x00, 0x00, 0x00, 0x68, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xB6 };

                int found = -1;
                for (int i = 0; i <= data.Length - oldPat.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < oldPat.Length; j++)
                    {
                        if (data[i + j] != oldPat[j]) { match = false; break; }
                    }
                    if (!match) continue;
                    if (found != -1)
                    {
                        Log("Splash AIR: patrón duplicado, se omite.");
                        return;
                    }
                    found = i;
                }

                if (found == -1)
                {
                    Log("Splash AIR: ya parcheado o runtime distinto.");
                    return;
                }

                string backup = dll + ".bak-nso-splash";
                if (!File.Exists(backup)) File.Copy(dll, backup, true);
                Array.Copy(newPat, 0, data, found, newPat.Length);
                File.WriteAllBytes(dll, data);
                Log("Splash AIR neutralizado (ventana 1x1).");
            }
            catch (Exception ex)
            {
                Log("Splash AIR: no se pudo parchear: " + ex.Message);
            }
        }

        private void Folder_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Elegí la carpeta donde instalar Ninja Saga PvP";
                dlg.SelectedPath = _installDir;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _installDir = dlg.SelectedPath;
                    SaveConfig();
                    Log("Carpeta de instalación: " + _installDir);
                    UpdateUi();
                }
            }
        }

        private void Open_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_installDir);
                Process.Start("explorer.exe", _installDir);
            }
            catch (Exception ex) { Log("Abrir carpeta: " + ex.Message); }
        }

        private void EnsureShortcut()
        {
            try
            {
                string launcherCopy = Path.Combine(_installDir, "NinjaSagaPvP-Launcher.exe");
                if (!string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(launcherCopy), StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Copy(Application.ExecutablePath, launcherCopy, true); } catch { }
                }

                string target = File.Exists(launcherCopy) ? launcherCopy : Application.ExecutablePath;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = Path.Combine(desktop, "Ninja Saga PvP.lnk");
                CreateShortcut(lnk, target, _installDir);
                Log("Acceso directo creado: " + lnk);
            }
            catch (Exception ex) { Log("Acceso directo: " + ex.Message); }
        }

        private static void CreateShortcut(string lnk, string target, string workdir)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type st = shortcut.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workdir });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { target + ",0" });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Ninja Saga PvP" });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _primary.Enabled = !busy;
            _folder.Enabled = !busy;
            _open.Enabled = !busy;
            if (!string.IsNullOrEmpty(status)) _status.Text = status;
            if (!busy) { /* la barra conserva el último valor */ }
        }

        private void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + msg;
            try { File.AppendAllText(Path.Combine(_dataDir, "launcher.log"), line + Environment.NewLine); } catch { }
            if (_log == null) return;
            if (_log.InvokeRequired) _log.BeginInvoke(new Action(delegate { AppendLog(line); }));
            else AppendLog(line);
        }

        private void AppendLog(string line)
        {
            _log.AppendText(line + Environment.NewLine);
        }

        private static string FormatGb(long bytes)
        {
            return string.Format("{0:N2} GB", bytes / 1073741824.0);
        }
    }
}
