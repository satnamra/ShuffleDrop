using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Krypton.Toolkit;

namespace ShuffleDrop
{
    internal static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            if(args.Length==2&&args[0]=="--make-assets") { Directory.CreateDirectory(args[1]); Art.SaveIcon(Path.Combine(args[1],"ShuffleDrop.ico")); using(var logo=Art.Logo(256))logo.Save(Path.Combine(args[1],"ShuffleDrop.png")); return; }
            bool created;
            bool rendering=args.Length>=2&&args[0]=="--render-preview";
            using (var mutex = new Mutex(true, rendering ? "Local\\ShuffleDrop.Preview" : "Local\\ShuffleDrop.Desktop", out created))
            {
                if (!created) { MessageBox.Show("ShuffleDrop is already running."); return; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { MessageBox.Show(e.Exception.Message, "ShuffleDrop", MessageBoxButtons.OK, MessageBoxIcon.Error); };
                using (var form = new MainForm())
                {
                    if (rendering)
                    {
                        form.PreparePreview(); if(args.Length>=3)form.SetTheme(args[2]!="light",false); form.Show(); Application.DoEvents();
                        using (var bitmap = new Bitmap(form.Width, form.Height))
                        { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.GetFullPath(args[1])); }
                        form.Close(); return;
                    }
                    Application.Run(form);
                }
            }
        }
    }
    internal sealed class Work
    {
        public string Kind, Directory;
        public Device Device;
        public LibrarySnapshot Snapshot;
        public string[] Files, Order;
        public LibraryTrack[] Tracks;
    }
    internal sealed partial class MainForm : Form
    {
        Color ink { get { return Theme.Text; } }
        Color teal { get { return Theme.Accent; } }
        readonly KryptonComboBox devices = new KryptonComboBox();
        readonly KryptonTextBox search = new KryptonTextBox();
        readonly LinkLabel batteryInfo = new LinkLabel();
        readonly ToolTip batteryTip = new ToolTip();
        readonly Label deviceInfo = new Label(), status = new Label(), totals = new Label(), selectedInfo = new Label(), playing = new Label(), queueSummary = new Label();
        readonly MusicList library = new MusicList(), queue = new MusicList();
        readonly PageHost tabs = new PageHost();
        readonly ContentPage libraryTab = new ContentPage("iPod library"), queueTab = new ContentPage("Add music");
        readonly WorkProgress progress = new WorkProgress();
        readonly UiButton refresh = new UiButton(), export = new UiButton(), delete = new UiButton(), up = new UiButton(), down = new UiButton(), saveOrder = new UiButton(), undo = new UiButton(), play = new UiButton(), pause = new UiButton(), stop = new UiButton(), add = new UiButton(), folder = new UiButton(), dropQueue = new UiButton(), send = new UiButton(), cancel = new UiButton();
        readonly BackgroundWorker worker = new BackgroundWorker();
        readonly AudioPreview audio = new AudioPreview();
        readonly List<string> files = new List<string>();
        List<LibraryTrack> ordered = new List<LibraryTrack>();
        LibrarySnapshot snapshot;
        Work active;
        bool busy, dirty, changingDevices, preview;
        volatile bool cancellation;
        int sortColumn; bool descending;
        string afterLoadStatus;
        Device CurrentDevice { get { return devices.SelectedItem as Device; } }
        LibraryTrack[] Selected { get { return library.SelectedItems.Cast<ListViewItem>().Select(row => (LibraryTrack)row.Tag).ToArray(); } }
        void ReloadDevices()
        {
            if (busy) return; StopAudio(); string root = CurrentDevice == null ? null : CurrentDevice.Root;
            changingDevices = true; devices.Items.Clear(); foreach (var d in Device.Find()) devices.Items.Add(d);
            int index = devices.Items.Cast<Device>().ToList().FindIndex(d => d.Root == root); if (devices.Items.Count > 0) devices.SelectedIndex = Math.Max(0, index); changingDevices = false;
            snapshot = null; ordered.Clear(); dirty = false; RenderLibrary(); UpdateDevice();
            if (!preview) LoadLibrary();
        }
        void UpdateDevice()
        {
            var d = CurrentDevice; deviceInfo.Text = d == null ? "No device found" : "Free: " + (d.Free / 1073741824.0).ToString("0.00") + " GB";
            // USB presence and disk power state do not establish battery charge or charging.
            // No verified Shuffle battery reader is available. Never substitute PC battery data.
            batteryInfo.Text = d == null ? "Battery: not connected" : "Battery % unavailable ⓘ";
            batteryInfo.Enabled = d != null;
            batteryTip.SetToolTip(batteryInfo, d == null ? "Connect your iPod and refresh the device list." : "Battery percentage and charging status cannot be read reliably. Click to learn how to check the battery on your iPod.");
            if (snapshot == null) totals.Text = d == null ? "Connect your iPod and click Refresh." : d.Label + "  •  Reading library…";
            else totals.Text = snapshot.Tracks.Count + " tracks  •  " + (snapshot.Tracks.Sum(t => t.Size) / 1048576.0).ToString("0.0") + " MB  •  " + TimeSpan.FromSeconds(snapshot.Tracks.Sum(t => t.Duration.TotalSeconds)).ToString(@"hh\:mm\:ss") + " of music";
            UpdateButtons();
        }
        void LoadLibrary()
        {
            if (busy || preview) return; StopAudio(); snapshot = null; ordered.Clear(); dirty = false; RenderLibrary(); UpdateDevice();
            var d = CurrentDevice; if (d == null) return;
            if (new Transfer(d, (p, s) => { }, () => false).HasPending) { status.Text = "An interrupted operation was found. Click Restore."; totals.Text = "The library needs to be restored."; return; }
            Start(new Work { Kind = "load", Device = d });
        }
        void Start(Work work)
        {
            if (busy || work.Device == null) return; StopAudio(); active = work; cancellation = false; busy = true; progress.Value = 0; UpdateButtons(); worker.RunWorkerAsync(work);
        }
        void DoWork(object sender, DoWorkEventArgs e)
        {
            var w = (Work)e.Argument; Action<int, string> report = (p, s) => worker.ReportProgress(p, s); var transfer = new Transfer(w.Device, report, () => cancellation);
            if (w.Kind == "load") e.Result = LibrarySnapshot.Read(w.Device, report, () => cancellation);
            else if (w.Kind == "add") e.Result = transfer.Add(w.Files);
            else if (w.Kind == "remove" || w.Kind == "order") e.Result = transfer.Manage(w.Snapshot.Database, w.Order, w.Kind == "remove");
            else if (w.Kind == "restore") transfer.Restore();
            else if (w.Kind == "export") e.Result = LibrarySnapshot.Export(w.Device, w.Tracks, w.Directory, report, () => cancellation);
        }
        void Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            busy = false; var work = active; UpdateButtons();
            if (e.Error != null)
            {
                status.Text = e.Error is OperationCanceledException ? "Operation canceled." : e.Error.Message;
                if (!(e.Error is OperationCanceledException)) ShowError(e.Error.Message);
                if (work.Kind == "load") totals.Text = "Could not read the library. Click Refresh to try again.";
                return;
            }
            progress.Value = 100;
            if (work.Kind == "load")
            {
                snapshot = (LibrarySnapshot)e.Result; ordered = snapshot.Tracks.ToList(); dirty = false; RenderLibrary(); UpdateDevice();
                status.Text = afterLoadStatus ?? "Library ready. Double-click a track to listen."; afterLoadStatus = null;
            }
            else if (work.Kind == "export") status.Text = "Tracks exported to your computer: " + e.Result + ".";
            else
            {
                if (work.Kind == "add") { var r = (TransferResult)e.Result; afterLoadStatus = "Added: " + r.Added + ". Already on your iPod: " + r.Skipped + "."; files.Clear(); RenderQueue(); SwitchPage(false); }
                else afterLoadStatus = work.Kind == "remove" ? "Tracks removed. Recovery copies are saved on your computer." : work.Kind == "order" ? "Track order saved." : "Previous library restored.";
                dirty = false; ReloadDevices();
            }
        }
        void RenderLibrary()
        {
            var selected = new HashSet<string>(Selected.Select(t => t.Relative)); library.BeginUpdate(); library.Items.Clear();
            string query = search.Text.Trim(); IEnumerable<LibraryTrack> rows = ordered.Where(t => query.Length == 0 || (t.Title + " " + t.Artist + " " + t.Album).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
            Func<LibraryTrack, object> key = t => sortColumn == 0 ? (object)ordered.IndexOf(t) : sortColumn == 1 ? t.Title : sortColumn == 2 ? t.Artist : sortColumn == 3 ? t.Album : sortColumn == 4 ? (object)t.Duration : t.Size;
            rows = descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
            foreach (var t in rows)
            {
                var row = new ListViewItem((ordered.IndexOf(t) + 1).ToString()) { Tag = t, ToolTipText = t.Error.Length > 0 ? t.Error : t.FullPath };
                row.SubItems.Add(t.Missing ? "[Missing] " + t.Title : t.Title); row.SubItems.Add(t.Artist); row.SubItems.Add(t.Album); row.SubItems.Add(t.DurationText); row.SubItems.Add((t.Size / 1048576.0).ToString("0.0") + " MB");
                if (t.Missing) row.ForeColor = Theme.Danger; library.Items.Add(row); row.Selected = selected.Contains(t.Relative);
            }
            library.EndUpdate(); UpdateButtons();
        }
        void UpdateButtons()
        {
            bool ready = !busy && snapshot != null; bool selected = Selected.Length > 0;
            devices.Enabled = !busy && !dirty; refresh.Enabled = !busy; search.Enabled = !busy;
            add.Enabled = folder.Enabled = dropQueue.Enabled = !busy; queue.AllowDrop = !busy;
            send.Enabled = !busy && !dirty && CurrentDevice != null && files.Count > 0;
            send.Primary = true;
            export.Enabled = ready && selected && !Selected.Any(t => t.Missing); delete.Enabled = ready && selected && !dirty;
            up.Enabled = down.Enabled = ready && selected && search.Text.Trim().Length == 0 && sortColumn == 0 && !descending;
            saveOrder.Enabled = ready && dirty; undo.Enabled = !busy && CurrentDevice != null && !dirty;
            play.Enabled = ready && Selected.Length == 1 && !Selected[0].Missing; pause.Enabled = stop.Enabled = audio.Opened && !busy;
            cancel.Visible = busy; cancel.Enabled = busy && !cancellation;
            progress.Visible = busy;
            selectedInfo.Text = "Showing: " + library.Items.Count + "  •  Selected: " + Selected.Length + (dirty ? "  •  Order changed — click Save order." : "  •  Ctrl+A selects all. Click a column header to sort.");
        }
        void PlaySelected()
        {
            if (busy || Selected.Length != 1 || snapshot == null) return;
            try
            {
                var t = Selected[0]; audio.Play(snapshot.Device.PathOf(t.Relative.Substring(1)));
                playing.Text = t.Title; artistPlaying.Text = t.Artist.Length > 0 ? t.Artist : "Unknown artist";
                LoadCover(t.FullPath); UpdateButtons(); UpdatePlayback();
            }
            catch (Exception e) { ShowError(e.Message); }
        }
        void StopAudio()
        {
            audio.Dispose(); playing.Text = "Choose a track"; artistPlaying.Text = "Listen on your computer";
            if (elapsed != null) elapsed.Text = "0:00 / 0:00";
            if (seek != null) seek.Fraction = 0;
            UpdateButtons();
        }
        void ExportTracks()
        {
            if (busy || snapshot == null || Selected.Length == 0) return;
            using (var d = new FolderBrowserDialog { Description = "Choose a folder on your computer for the selected tracks." }) if (d.ShowDialog(this) == DialogResult.OK)
                Start(new Work { Kind = "export", Device = snapshot.Device, Tracks = Selected, Directory = d.SelectedPath });
        }
        void RemoveTracks()
        {
            if (busy || snapshot == null || Selected.Length == 0 || dirty) return;
            var selected = Selected; long size = selected.Sum(t => t.Size);
            string names = String.Join("\n", selected.Take(5).Select(t => "• " + t.Title)); if (selected.Length > 5) names += "\n…";
            if (MessageBox.Show(this, "Remove " + selected.Length + " tracks from your iPod (" + (size / 1048576.0).ToString("0.0") + " MB)?\n\n" + names + "\n\nRecovery copies will be saved and verified on this computer before removal. Use Restore to recover the tracks.", "Remove selected tracks", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var paths = new HashSet<string>(selected.Select(t => t.Relative));
            Start(new Work { Kind = "remove", Device = snapshot.Device, Snapshot = snapshot, Order = ordered.Where(t => !paths.Contains(t.Relative)).Select(t => t.Relative).ToArray() });
        }
        void MoveSelected(int direction)
        {
            if (busy || snapshot == null || search.Text.Trim().Length != 0 || sortColumn != 0 || descending) return;
            var selected = new HashSet<LibraryTrack>(Selected); bool moved = false;
            if (direction < 0) for (int i = 1; i < ordered.Count; i++) { if (selected.Contains(ordered[i]) && !selected.Contains(ordered[i - 1])) { var t = ordered[i]; ordered[i] = ordered[i - 1]; ordered[i - 1] = t; moved = true; } }
            else for (int i = ordered.Count - 2; i >= 0; i--) { if (selected.Contains(ordered[i]) && !selected.Contains(ordered[i + 1])) { var t = ordered[i]; ordered[i] = ordered[i + 1]; ordered[i + 1] = t; moved = true; } }
            if (moved) dirty = true; RenderLibrary();
        }
        bool DiscardOrder()
        { if (!dirty) return true; if (MessageBox.Show(this, "You have unsaved track-order changes. Discard them?", "Track order", MessageBoxButtons.YesNo) != DialogResult.Yes) return false; dirty = false; return true; }
        void AddPaths(IEnumerable<string> paths)
        {
            int skipped = 0; var stack = new Stack<string>(paths.Reverse());
            while (stack.Count > 0)
            {
                string p = stack.Pop(); try
                {
                    if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                    if (Directory.Exists(p)) { foreach (string child in Directory.GetFileSystemEntries(p).OrderByDescending(x => x)) stack.Push(child); }
                    else if (Path.GetExtension(p).Equals(".mp3", StringComparison.OrdinalIgnoreCase)) { p = Path.GetFullPath(p); if (!files.Contains(p, StringComparer.OrdinalIgnoreCase)) files.Add(p); }
                    else skipped++;
                    if (files.Count >= 10000) break;
                }
                catch (IOException) { skipped++; } catch (UnauthorizedAccessException) { skipped++; }
            }
            RenderQueue(); status.Text = "Added to queue. Unsupported or inaccessible files skipped: " + skipped + ".";
        }
        void RenderQueue()
        {
            queue.BeginUpdate(); queue.Items.Clear(); long size = 0;
            foreach (string f in files) { long n = 0; try { n = new FileInfo(f).Length; } catch (IOException) { } var row = new ListViewItem(Path.GetFileNameWithoutExtension(f)) { Tag = f, ToolTipText = f }; row.SubItems.Add((n / 1048576.0).ToString("0.0") + " MB"); queue.Items.Add(row); size += n; }
            queue.EndUpdate(); queueSummary.Text = files.Count + " tracks queued  •  " + (size / 1048576.0).ToString("0.0") + " MB"; queueTab.Text = "Add music" + (files.Count > 0 ? " (" + files.Count + ")" : ""); UpdateButtons();
        }
        void ShowError(string message) { MessageBox.Show(this, message, "ShuffleDrop", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        void ShowBatteryHelp()
        {
            MessageBox.Show(this,
                "BATTERY PERCENTAGE UNAVAILABLE\nThis app cannot reliably read your Shuffle's battery percentage or confirm whether it is charging. An unknown level does not mean the battery is empty.\n\n" +
                "4TH-GENERATION SHUFFLE\nSafely eject the player using Windows and unplug its USB cable. When switched on: green means 50–100%, orange 25–49%, and red less than 25%. With headphones connected, press the VoiceOver button twice to hear an approximate battery level.\n\n" +
                "2ND-GENERATION SHUFFLE\nDisconnect the cable, or quickly switch the player off and on: green means 31–100%, orange 10–30%, and red less than 10%.\n\n" +
                "4TH-GENERATION SHUFFLE CONNECTED TO A COMPUTER\nGreen means charged; solid orange means charging. Flashing orange indicates syncing or disk use, not a specific battery percentage.\n\n" +
                "Source: Apple Support · support.apple.com/102158",
                "How to check your iPod battery", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void Help()
        {
            MessageBox.Show(this, "LIBRARY\nBrowse tracks, artists, albums, durations, and sizes. Search filters the list. Ctrl+A selects all visible tracks.\n\nMANAGE MUSIC\nExport saves selected tracks to your computer with readable filenames. Remove frees device space after saving and verifying recovery copies. Restore undoes the latest operation; later changes replace that recovery point. Older backups remain in %LOCALAPPDATA%\\ShuffleDrop\\Backups.\n\nTRACK ORDER\nSort by # in ascending order, clear the search, use the arrow buttons, then click Save order. This order does not apply in shuffle mode.\n\nADD MUSIC\nOpen Add music, choose MP3 files, and click Transfer to iPod. Existing tracks are preserved. M4A/FLAC conversion and new VoiceOver announcements are not supported. Existing files can be viewed and exported.\n\nSafely eject your iPod using Windows. This experimental app does not restore firmware or update iTunesDB. Other syncing apps can replace the library.\n\nMetadata uses TagLibSharp 2.3.0 (LGPL-2.1). Its license and corresponding source files are included in the app folder.", "ShuffleDrop 0.3.1 • Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        internal void PreparePreview()
        {
            preview = true; ReloadDevices();
            if (CurrentDevice != null) { snapshot = LibrarySnapshot.Read(CurrentDevice, (p, s) => { }, () => false); ordered = snapshot.Tracks.ToList(); RenderLibrary(); UpdateDevice(); status.Text = "Library ready. Double-click a track to listen."; }
        }
    }
}
