using System;
using System.Collections.Generic;
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

[assembly: AssemblyTitle("OBS Performance Test 2 Setup")]
[assembly: AssemblyProduct("OBS Performance Test 2")]
[assembly: AssemblyDescription("Installs the Fiztba performance test build based on OBS Studio 33.0.1")]
[assembly: AssemblyVersion("33.0.1.2")]
[assembly: AssemblyFileVersion("33.0.1.2")]

namespace PerformanceTestSetup
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
        private const long ArchiveSize = 223786669;
        internal const string Folder = "OBS-Performance-Test-33.0.1-2";
        internal static readonly bool Bundled = Array.IndexOf(Assembly.GetExecutingAssembly().GetManifestResourceNames(), "test2.upstream") >= 0;
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
            request.UserAgent = "Fiztba-Performance-Test-2-Setup";
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
            var replacedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string[] module in BuildInput.Modules)
                replacedSymbols.Add(Path.ChangeExtension(module[0], ".pdb").Replace('\\', '/'));
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                int index = 0, last = -1;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    cancel.ThrowIfCancellationRequested();
                    if (replacedSymbols.Contains(entry.FullName)) continue;
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
                    int percent = (Bundled ? 5 : 65) + (++index * (Bundled ? 92 : 32) / archive.Entries.Count);
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

        private static Stream OpenArchive(string scratch, Action<int, string> status, CancellationToken cancel)
        {
            if (Bundled) return Assembly.GetExecutingAssembly().GetManifestResourceStream("test2.upstream");
            string zip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Archive);
            if (!File.Exists(zip))
            {
                zip = Path.Combine(scratch, "upstream.zip");
                status(0, "Connecting to the official OBS Studio release…");
                Download(zip, status, cancel);
            }
            return File.OpenRead(zip);
        }

        private static void CreateShortcut(string payload, string target)
        {
            string bin = Path.Combine(target, "bin", "64bit");
            string obs = Path.Combine(bin, "obs64.exe");
            Type type = Type.GetTypeFromProgID("WScript.Shell", true);
            dynamic shell = Activator.CreateInstance(type);
            try
            {
                dynamic shortcut = shell.CreateShortcut(Path.Combine(payload, "Launch OBS Performance Test 2.lnk"));
                try
                {
                    shortcut.TargetPath = obs;
                    shortcut.WorkingDirectory = bin;
                    shortcut.Arguments = "--portable --disable-updater";
                    shortcut.Description = "Launch the OBS Studio 33.0.1 performance test fork";
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
            string scratch = Path.Combine(parent, ".obs-performance-test-2-setup-" + Guid.NewGuid().ToString("N"));
            string payload = Path.Combine(scratch, "payload");
            Directory.CreateDirectory(payload);
            try
            {
                using (Stream input = OpenArchive(scratch, status, cancel))
                {
                    status(Bundled ? 0 : 61, "Checking the official OBS release archive…");
                    if (input.Length != ArchiveSize)
                        throw new InvalidDataException("The official OBS Studio archive must be 223,786,669 bytes.");
                    CheckHash(input, ArchiveHash, Archive);
                    input.Position = 0;
                    Unpack(input, payload, status, cancel);
                }
                status(98, "Installing the four fork modules and portable launcher…");
                foreach (string[] module in BuildInput.Modules)
                {
                    cancel.ThrowIfCancellationRequested();
                    string path = Path.Combine(payload, module[0]);
                    using (FileStream input = File.OpenRead(path)) CheckHash(input, module[1], module[0] + " (upstream)");
                    WriteResource("test2." + Path.GetFileName(path), path, FileMode.Truncate);
                    using (FileStream input = File.OpenRead(path)) CheckHash(input, module[2], module[0] + " (fork)");
                }
                WriteResource("test2.notice", Path.Combine(payload, "Performance-Test-2-NOTICE.txt"), FileMode.CreateNew);
                WriteResource("test2.license", Path.Combine(payload, "Performance-Test-2-COPYING.txt"), FileMode.CreateNew);
                WriteResource("test2.credits", Path.Combine(payload, "Performance-Test-2-Third-Party-Notices.zip"), FileMode.CreateNew);
                File.WriteAllText(Path.Combine(payload, "portable_mode.txt"), "");
                File.WriteAllText(Path.Combine(payload, "disable_updater.txt"), "");
                CreateShortcut(payload, target);
                cancel.ThrowIfCancellationRequested();
                // The new directory appears only after all files pass verification.
                // Move fails if another process populated the destination during setup.
                Directory.Move(payload, target);
                status(100, "OBS Performance Test 2 is ready.");
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
            Text = "OBS Performance Test 2 Setup";
            Font = SystemFonts.MessageBoxFont;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(585, 285);
            Label title = new Label { Text = "Install OBS Performance Test 2", AutoSize = true, Font = new Font(Font.FontFamily, 15, FontStyle.Bold) };
            Place(title, 18, 15, 530, 32);
            Place(new Label { Text = Installer.Bundled
                ? "This installer contains the full application and installs it in a separate folder with its own OBS settings."
                : "This installer downloads the official OBS Studio 33.0.1 archive (about 224 MB) and installs the fork in a separate folder. It can also use the official ZIP beside it." }, 20, 57, 545, 44);
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
                status.Text = "Installed. Open “Launch OBS Performance Test 2” in the new folder.";
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
