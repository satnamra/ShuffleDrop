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
                if (!created) { MessageBox.Show("„ShuffleDrop“ jau paleista."); return; }
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
        readonly ContentPage libraryTab = new ContentPage("iPod biblioteka"), queueTab = new ContentPage("Pridėti muziką");
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
            var d = CurrentDevice; deviceInfo.Text = d == null ? "Grotuvo nerasta" : "Laisva " + (d.Free / 1073741824.0).ToString("0.00") + " GB";
            // USB presence and disk power state do not establish battery charge or charging.
            // No verified Shuffle battery reader is available. Never substitute PC battery data.
            batteryInfo.Text = d == null ? "Baterija: neprijungta" : "Baterijos % nepalaikomas ⓘ";
            batteryInfo.Enabled = d != null;
            batteryTip.SetToolTip(batteryInfo, d == null ? "Prijunk iPod ir atnaujink sąrašą." : "Programa negauna patikimo baterijos procento ar įkrovimo būsenos. Spausk, kad sužinotum, kaip patikrinti įkrovą pačiame grotuve.");
            if (snapshot == null) totals.Text = d == null ? "Prijunk grotuvą ir spausk „Atnaujinti“." : d.Label + "  •  Skaitoma biblioteka…";
            else totals.Text = snapshot.Tracks.Count + " dainų  •  " + (snapshot.Tracks.Sum(t => t.Size) / 1048576.0).ToString("0.0") + " MB  •  " + TimeSpan.FromSeconds(snapshot.Tracks.Sum(t => t.Duration.TotalSeconds)).ToString(@"hh\:mm\:ss") + " muzikos";
            UpdateButtons();
        }
        void LoadLibrary()
        {
            if (busy || preview) return; StopAudio(); snapshot = null; ordered.Clear(); dirty = false; RenderLibrary(); UpdateDevice();
            var d = CurrentDevice; if (d == null) return;
            if (new Transfer(d, (p, s) => { }, () => false).HasPending) { status.Text = "Rastas nutrūkęs pakeitimas. Spausk „Atkurti biblioteką“."; totals.Text = "Biblioteką reikia atkurti."; return; }
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
                status.Text = e.Error is OperationCanceledException ? "Operacija atšaukta." : e.Error.Message;
                if (!(e.Error is OperationCanceledException)) ShowError(e.Error.Message);
                if (work.Kind == "load") totals.Text = "Nepavyko perskaityti bibliotekos. Spausk „Atnaujinti“.";
                return;
            }
            progress.Value = 100;
            if (work.Kind == "load")
            {
                snapshot = (LibrarySnapshot)e.Result; ordered = snapshot.Tracks.ToList(); dirty = false; RenderLibrary(); UpdateDevice();
                status.Text = afterLoadStatus ?? "Biblioteka paruošta. Dukart spustelėk dainą, kad klausytum."; afterLoadStatus = null;
            }
            else if (work.Kind == "export") status.Text = "Į kompiuterį išsaugota dainų: " + e.Result + ".";
            else
            {
                if (work.Kind == "add") { var r = (TransferResult)e.Result; afterLoadStatus = "Pridėta: " + r.Added + ". Jau buvo grotuve: " + r.Skipped + "."; files.Clear(); RenderQueue(); SwitchPage(false); }
                else afterLoadStatus = work.Kind == "remove" ? "Dainos pašalintos. Jų atkūrimo kopijos saugomos kompiuteryje." : work.Kind == "order" ? "Dainų tvarka išsaugota." : "Ankstesnė biblioteka atkurta.";
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
                row.SubItems.Add(t.Missing ? "[Trūksta] " + t.Title : t.Title); row.SubItems.Add(t.Artist); row.SubItems.Add(t.Album); row.SubItems.Add(t.DurationText); row.SubItems.Add((t.Size / 1048576.0).ToString("0.0") + " MB");
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
            selectedInfo.Text = "Rodoma: " + library.Items.Count + "  •  Pasirinkta: " + Selected.Length + (dirty ? "  •  Tvarka pakeista — spausk „Išsaugoti tvarką“." : "  •  Ctrl+A pažymi visas. Stulpelio antraštė rūšiuoja vaizdą.");
        }
        void PlaySelected()
        {
            if (busy || Selected.Length != 1 || snapshot == null) return;
            try
            {
                var t = Selected[0]; audio.Play(snapshot.Device.PathOf(t.Relative.Substring(1)));
                playing.Text = t.Title; artistPlaying.Text = t.Artist.Length > 0 ? t.Artist : "Nežinomas atlikėjas";
                LoadCover(t.FullPath); UpdateButtons(); UpdatePlayback();
            }
            catch (Exception e) { ShowError(e.Message); }
        }
        void StopAudio()
        {
            audio.Dispose(); playing.Text = "Pasirink dainą"; artistPlaying.Text = "Klausyk per kompiuterį";
            if (elapsed != null) elapsed.Text = "0:00 / 0:00";
            if (seek != null) seek.Fraction = 0;
            UpdateButtons();
        }
        void ExportTracks()
        {
            if (busy || snapshot == null || Selected.Length == 0) return;
            using (var d = new FolderBrowserDialog { Description = "Kur kompiuteryje išsaugoti pasirinktas dainas?" }) if (d.ShowDialog(this) == DialogResult.OK)
                Start(new Work { Kind = "export", Device = snapshot.Device, Tracks = Selected, Directory = d.SelectedPath });
        }
        void RemoveTracks()
        {
            if (busy || snapshot == null || Selected.Length == 0 || dirty) return;
            var selected = Selected; long size = selected.Sum(t => t.Size);
            string names = String.Join("\n", selected.Take(5).Select(t => "• " + t.Title)); if (selected.Length > 5) names += "\n…";
            if (MessageBox.Show(this, "Pašalinti " + selected.Length + " dainų iš iPod (" + (size / 1048576.0).ToString("0.0") + " MB)?\n\n" + names + "\n\nPrieš šalinimą bus patikrintos ir išsaugotos dainų kopijos šiame kompiuteryje. Jas galėsi grąžinti per „Atkurti biblioteką“.", "Šalinti pasirinktas dainas", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
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
        { if (!dirty) return true; if (MessageBox.Show(this, "Yra neišsaugotų dainų tvarkos pakeitimų. Atmesti juos?", "Dainų tvarka", MessageBoxButtons.YesNo) != DialogResult.Yes) return false; dirty = false; return true; }
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
            RenderQueue(); status.Text = "Pridėta į eilę. Praleista kitų arba nepasiekiamų failų: " + skipped + ".";
        }
        void RenderQueue()
        {
            queue.BeginUpdate(); queue.Items.Clear(); long size = 0;
            foreach (string f in files) { long n = 0; try { n = new FileInfo(f).Length; } catch (IOException) { } var row = new ListViewItem(Path.GetFileNameWithoutExtension(f)) { Tag = f, ToolTipText = f }; row.SubItems.Add((n / 1048576.0).ToString("0.0") + " MB"); queue.Items.Add(row); size += n; }
            queue.EndUpdate(); queueSummary.Text = files.Count + " dainų eilėje  •  " + (size / 1048576.0).ToString("0.0") + " MB"; queueTab.Text = "Pridėti muziką" + (files.Count > 0 ? " (" + files.Count + ")" : ""); UpdateButtons();
        }
        void ShowError(string message) { MessageBox.Show(this, message, "ShuffleDrop", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        void ShowBatteryHelp()
        {
            MessageBox.Show(this,
                "BATERIJOS PROCENTAS NEPASIEKIAMAS\nŠi programa neturi patikimo būdo nuskaityti „Shuffle“ baterijos procentą ar patvirtinti, ar jis dabar kraunasi. „Lygis nežinomas“ nereiškia, kad baterija tuščia.\n\n" +
                "4 KARTOS SHUFFLE\nSaugiai išimk grotuvą per Windows ir atjunk USB laidą. Įjungus: žalia lemputė – 50–100 %, oranžinė – 25–49 %, raudona – mažiau nei 25 %. Prijungęs ausines du kartus spausk VoiceOver mygtuką – išgirsi apytikslę įkrovą.\n\n" +
                "2 KARTOS SHUFFLE\nAtjungus nuo laido arba greitai išjungus ir vėl įjungus: žalia – 31–100 %, oranžinė – 10–30 %, raudona – mažiau nei 10 %.\n\n" +
                "4 KARTOS GROTUVAS PRIJUNGTAS PRIE KOMPIUTERIO\nŽalia – įkrautas; nuolat oranžinė – kraunasi. Mirksinti oranžinė reiškia sinchronizavimą arba disko režimą, o ne konkretų baterijos procentą.\n\n" +
                "Šaltinis: Apple Support · support.apple.com/102158",
                "Kaip patikrinti iPod bateriją", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void Help()
        {
            MessageBox.Show(this, "BIBLIOTEKA\nMatysi grotuve esančias dainas, atlikėjus, albumus, trukmę ir dydį. Paieška filtruoja sąrašą. Ctrl+A pažymi visas matomas dainas.\n\nTVARKYMAS\n„Į kompiuterį“ išsaugo pasirinktas dainas su suprantamais pavadinimais. „Šalinti iš iPod“ atlaisvina vietą tik sukūrusi dainų kopijas kompiuteryje. Atkuriama paskutinė operacija; vėlesni pakeitimai pakeičia atkūrimo tašką. Senos kopijos lieka %LOCALAPPDATA%\\ShuffleDrop\\Backups.\n\nTVARKA\nGrąžink rūšiavimą pagal # didėjimo tvarka, išvalyk paiešką, naudok ↑ / ↓ ir „Išsaugoti tvarką“. Atsitiktinio grojimo režime ši tvarka netaikoma.\n\nPRIDĖJIMAS\nKortelėje „Pridėti muziką“ pridėk MP3 ir spausk „Perkelti į iPod“. Esamos dainos išsaugomos. Nauji M4A / FLAC kol kas nekonvertuojami; esamus failus galima matyti ir eksportuoti. VoiceOver naujoms dainoms negeneruojamas.\n\nSaugiai išimk grotuvą per Windows. Ši bandomoji versija neatkuria gamyklinės programinės įrangos ir neredaguoja iTunesDB. Kitos sinchronizavimo programos gali pakeisti biblioteką.\n\nDainų informacijai naudojama TagLibSharp 2.3.0 (LGPL-2.1). Licencija ir bibliotekos šaltiniai pateikti programos aplanke.", "ShuffleDrop 0.3 • Pagalba", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        internal void PreparePreview()
        {
            preview = true; ReloadDevices();
            if (CurrentDevice != null) { snapshot = LibrarySnapshot.Read(CurrentDevice, (p, s) => { }, () => false); ordered = snapshot.Tracks.ToList(); RenderLibrary(); UpdateDevice(); status.Text = "Biblioteka paruošta. Dukart spustelėk dainą, kad klausytum."; }
        }
    }
}
