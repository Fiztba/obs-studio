using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("OBS WGC Test 1 Setup")]
[assembly: AssemblyProduct("OBS WGC Test 1")]
[assembly: AssemblyDescription("Installs the Fiztba OBS Studio Windows Capture test fork")]
[assembly: AssemblyVersion("33.0.1.1")]
[assembly: AssemblyFileVersion("33.0.1.1")]

namespace WgcTestSetup
{
    internal sealed class ExistingDestination : IOException
    {
        public ExistingDestination() : base("This folder already exists. Choose a new folder to keep your existing files and OBS settings.") { }
    }

    internal static class Installer
    {
        internal const string Archive = "OBS-Studio-33.0.1-Windows-x64.zip";
        internal const string Url = "https://github.com/obsproject/obs-studio/releases/download/33.0.1/" + Archive;
        internal const string ArchiveHash = "0d9436451c2d772b33428e3fdae9776f071895c335de17283176306fb72651d3";
        internal const string OriginalModuleHash = "4777e510d6436c3a3496d7ac4c360b3147441c74da2ea0856a3f65ce26af57a1";
        private const long ArchiveSize = 223786669;
        internal const string Folder = "OBS-WGC-Test-33.0.1-1";
        private const string Module = @"bin\64bit\libobs-winrt.dll";
        internal static readonly string DefaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Folder);

        private static string Digest(Stream stream)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void CheckHash(Stream stream, string expected, string name)
        {
            if (!String.Equals(Digest(stream), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SHA-256 verification failed for " + name + ". The test build was not installed.");
        }

        private static void Download(string file, Action<int, string> status, CancellationToken cancel)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
            request.UserAgent = "Fiztba-OBS-WGC-Test-1-Setup";
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;
            using (cancel.Register(request.Abort))
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.ResponseUri.Scheme != Uri.UriSchemeHttps)
                    throw new IOException("The OBS archive download must use HTTPS.");
                using (Stream network = response.GetResponseStream())
                using (FileStream output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] block = new byte[131072];
                    int last = -1;
                    int count;
                    while ((count = network.Read(block, 0, block.Length)) > 0)
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (output.Length + count > ArchiveSize)
                            throw new InvalidDataException("The download exceeds the official archive size.");
                        output.Write(block, 0, count);
                        int percent = (int)(output.Length * 60 / ArchiveSize);
                        if (percent != last)
                            status(percent, "Downloading OBS Studio 33.0.1 from GitHub…");
                        last = percent;
                    }
                }
            }
        }

        private static void Unpack(Stream stream, string payload, Action<int, string> status, CancellationToken cancel)
        {
            string prefix = Path.GetFullPath(payload) + Path.DirectorySeparatorChar;
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                int index = 0, last = -1;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    cancel.ThrowIfCancellationRequested();
                    string name = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    string path = Path.GetFullPath(Path.Combine(payload, name));
                    if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(name) || name.Contains(":"))
                        throw new InvalidDataException("Invalid archive path: " + entry.FullName);
                    if (entry.Name.Length == 0)
                        Directory.CreateDirectory(path);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using (Stream src = entry.Open())
                        using (FileStream dst = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            src.CopyTo(dst);
                    }
                    int percent = 65 + (++index * 32 / archive.Entries.Count);
                    if (percent != last) status(percent, "Extracting the verified OBS release…");
                    last = percent;
                }
            }
        }

        private static void WriteResource(string name, string path, FileMode mode)
        {
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (input == null) throw new InvalidDataException("Missing packaged resource: " + name);
                using (FileStream output = new FileStream(path, mode, FileAccess.Write, FileShare.None))
                    input.CopyTo(output);
            }
        }

        private static void CreateShortcut(string payload, string target)
        {
            string bin = Path.Combine(target, "bin", "64bit");
            string obs = Path.Combine(bin, "obs64.exe");
            Type type = Type.GetTypeFromProgID("WScript.Shell", true);
            dynamic shell = Activator.CreateInstance(type);
            try
            {
                dynamic shortcut = shell.CreateShortcut(Path.Combine(payload, "Launch OBS WGC Test.lnk"));
                try
                {
                    shortcut.TargetPath = obs;
                    shortcut.WorkingDirectory = bin;
                    shortcut.Arguments = "--portable --disable-updater";
                    shortcut.Description = "Launch the OBS Studio 33.0.1 Windows Capture test fork";
                    shortcut.IconLocation = obs + ",0";
                    shortcut.Save();
                }
                finally { Marshal.FinalReleaseComObject(shortcut); }
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }

        internal static void Run(string destination, Action<int, string> status, CancellationToken cancel)
        {
            string target = Path.GetFullPath(destination.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parent = Path.GetDirectoryName(target);
            if (String.IsNullOrEmpty(parent) || !Path.IsPathRooted(destination))
                throw new ArgumentException("Choose a new folder using its full path.");
            if (Directory.Exists(target) || File.Exists(target)) throw new ExistingDestination();
            Directory.CreateDirectory(parent);
            string scratch = Path.Combine(parent, ".obs-wgc-setup-" + Guid.NewGuid().ToString("N"));
            string payload = Path.Combine(scratch, "payload");
            Directory.CreateDirectory(payload);
            try
            {
                string zip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Archive);
                if (!File.Exists(zip))
                {
                    zip = Path.Combine(scratch, "upstream.zip");
                    status(0, "Connecting to the official OBS Studio release…");
                    Download(zip, status, cancel);
                }
                else status(0, "Using the official archive beside this installer…");
                cancel.ThrowIfCancellationRequested();
                using (FileStream input = new FileStream(zip, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    status(61, "Checking the official archive size and SHA-256…");
                    if (input.Length != ArchiveSize)
                        throw new InvalidDataException("The official OBS Studio archive must be 223,786,669 bytes.");
                    CheckHash(input, ArchiveHash, Archive);
                    input.Position = 0;
                    Unpack(input, payload, status, cancel);
                }
                status(98, "Installing the tested capture module and portable launcher…");
                string module = Path.Combine(payload, Module);
                using (FileStream input = File.OpenRead(module)) CheckHash(input, OriginalModuleHash, "the stock capture module");
                WriteResource("wgc.native", module, FileMode.Truncate);
                using (FileStream input = File.OpenRead(module)) CheckHash(input, BuildInput.ModuleHash, "the test capture module");
                WriteResource("wgc.notice", Path.Combine(payload, "WGC-Test-1-NOTICE.txt"), FileMode.CreateNew);
                WriteResource("wgc.license", Path.Combine(payload, "WGC-Test-1-COPYING.txt"), FileMode.CreateNew);
                File.WriteAllText(Path.Combine(payload, "portable_mode.txt"), "");
                File.WriteAllText(Path.Combine(payload, "disable_updater.txt"), "");
                CreateShortcut(payload, target);
                cancel.ThrowIfCancellationRequested();
                // The new directory appears only after all files pass verification.
                // Move fails if another process populated the destination during setup.
                Directory.Move(payload, target);
                status(100, "OBS WGC Test 1 is ready.");
            }
            finally
            {
                try { Directory.Delete(scratch, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static void Launch(string destination)
        {
            string bin = Path.Combine(destination, "bin", "64bit");
            Process.Start(new ProcessStartInfo(Path.Combine(bin, "obs64.exe"), "--portable --disable-updater") {
                WorkingDirectory = bin, UseShellExecute = false
            });
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly TextBox target = new TextBox();
        private readonly Label status = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly CheckBox launch = new CheckBox();
        private readonly Button action = new Button(), browse = new Button(), cancel = new Button();
        private CancellationTokenSource running;
        private bool installed;

        public SetupForm()
        {
            Text = "OBS WGC Test 1 Setup";
            Font = SystemFonts.MessageBoxFont;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(585, 285);
            Label title = new Label { Text = "Install OBS WGC Test 1", AutoSize = true, Font = new Font(Font.FontFamily, 15, FontStyle.Bold) };
            Place(title, 18, 15, 530, 32);
            Place(new Label { Text = "This OBS Studio 33.0.1 test build uses a separate portable configuration. Setup downloads the official OBS archive (about 224 MB) or uses the matching ZIP beside this installer." }, 20, 57, 545, 44);
            Place(new Label { Text = "New installation folder" }, 20, 111, 400, 18);
            target.Text = Installer.DefaultPath;
            Place(target, 20, 133, 452, 27);
            browse.Text = "Browse…"; Place(browse, 480, 131, 85, 27);
            browse.Click += delegate {
                using (FolderBrowserDialog picker = new FolderBrowserDialog { Description = "Choose the parent folder for this test installation." })
                    if (picker.ShowDialog(this) == DialogResult.OK) target.Text = Path.Combine(picker.SelectedPath, Installer.Folder);
            };
            launch.Text = "Launch OBS when setup finishes"; launch.Checked = true;
            Place(launch, 20, 164, 525, 24);
            Place(status, 20, 193, 545, 20);
            Place(progress, 20, 217, 545, 8);
            action.Text = "Install"; cancel.Text = "Cancel";
            Place(action, 374, 242, 93, 29); Place(cancel, 476, 242, 89, 29);
            AcceptButton = action; CancelButton = cancel;
            action.Click += async delegate { await Install(); };
            cancel.Click += delegate {
                if (!installed) { Close(); return; }
                try { Process.Start(new ProcessStartInfo(target.Text) { UseShellExecute = true }); }
                catch (Win32Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (running != null) { running.Cancel(); status.Text = "Canceling setup…"; e.Cancel = true; }
            };
        }

        private void Place(Control c, int x, int y, int width, int height)
        {
            Controls.Add(c); c.SetBounds(x, y, width, height);
        }

        private async Task Install()
        {
            if (installed) { Close(); return; }
            string destination = target.Text.Trim();
            running = new CancellationTokenSource();
            target.Enabled = browse.Enabled = action.Enabled = launch.Enabled = false;
            Progress<Tuple<int,string>> updates = new Progress<Tuple<int,string>>(p => { status.Text = p.Item2; progress.Value = p.Item1; });
            IProgress<Tuple<int,string>> ui = updates;
            try
            {
                await Task.Run(() => Installer.Run(destination, (n,s) => ui.Report(Tuple.Create(n,s)), running.Token));
                installed = true;
                status.Text = "Installed. Open “Launch OBS WGC Test” in the new folder.";
                progress.Value = 100;
                cancel.Text = "Open folder"; action.Text = "Close"; CancelButton = action;
                if (launch.Checked) Installer.Launch(destination);
            }
            catch (Exception ex)
            {
                if (running.IsCancellationRequested) status.Text = "Setup canceled.";
                else
                {
                    status.Text = installed ? "Installation complete. Use the launcher in the new folder." : "Setup could not finish.";
                    MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                bool close = running.IsCancellationRequested;
                running.Dispose(); running = null;
                target.Enabled = true; target.ReadOnly = installed;
                browse.Enabled = launch.Enabled = !installed;
                action.Enabled = true;
                if (close) Close();
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length != 0)
            {
                if (args.Length != 3 || args[0] != "--silent" || args[1] != "--install-dir")
                {
                    Console.Error.WriteLine("Usage: --silent --install-dir <full path to a new folder>");
                    return 4;
                }
                try
                {
                    string previous = "";
                    Installer.Run(args[2], (n,s) => { if (s != previous) Console.WriteLine(s); previous = s; }, CancellationToken.None);
                    return 0;
                }
                catch (ExistingDestination ex) { Console.Error.WriteLine(ex.Message); return 2; }
                catch (InvalidDataException ex) { Console.Error.WriteLine(ex.Message); return 3; }
                catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
            return 0;
        }
    }
}
