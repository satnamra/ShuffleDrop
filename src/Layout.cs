using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Krypton.Toolkit;

namespace ShuffleDrop
{
    internal sealed partial class MainForm
    {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        readonly ThemeToggle themeToggle=new ThemeToggle();
        readonly UiButton navLibrary=new UiButton(), navQueue=new UiButton();
        readonly Label pageTitle=new Label(), pageHint=new Label(), artistPlaying=new Label(), elapsed=new Label(), volumeLabel=new Label();
        readonly PictureBox cover=new PictureBox();
        readonly SeekBar seek=new SeekBar(), volume=new SeekBar();
        readonly System.Windows.Forms.Timer playbackTimer=new System.Windows.Forms.Timer();
        readonly KryptonManager palette=new KryptonManager();
        bool applyingTheme;
        public MainForm()
        {
            Theme.Load(); Text="ShuffleDrop • Muzika tavo iPod";
            ClientSize=new Size(1300,800); MinimumSize=new Size(1120,710);
            StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
            string iconPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ShuffleDrop.ico"); if(File.Exists(iconPath))Icon=new Icon(iconPath);
            var shell=Grid(2,2); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,220));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,126));Controls.Add(shell);
            BuildSidebar(shell); BuildContent(shell); BuildPlayer(shell);
            refresh.Click+=delegate {if(DiscardOrder())ReloadDevices();};
            devices.SelectedIndexChanged+=delegate {if(!changingDevices)LoadLibrary();};
            cancel.Click+=delegate {cancellation=true;cancel.Enabled=false;status.Text="Baigiama operacija…";};
            play.Click+=delegate{PlaySelected();}; library.DoubleClick+=delegate{PlaySelected();};
            pause.Click+=delegate{try{audio.PauseResume();UpdatePlayback();}catch(Exception e){ShowError(e.Message);}};
            stop.Click+=delegate{StopAudio();UpdatePlayback();};
            worker.WorkerReportsProgress=true;worker.DoWork+=DoWork;
            worker.ProgressChanged+=delegate(object s,ProgressChangedEventArgs e){progress.Value=Math.Max(0,Math.Min(100,e.ProgressPercentage));status.Text=(string)e.UserState;};
            worker.RunWorkerCompleted+=Completed;
            themeToggle.Checked=Theme.Dark; themeToggle.CheckedChanged+=delegate{if(applyingTheme)return;SetTheme(themeToggle.Checked,true);};
            playbackTimer.Interval=250;playbackTimer.Tick+=delegate{UpdatePlayback();};playbackTimer.Start();
            Shown+=delegate{SetTitleTheme();if(!preview)ReloadDevices();};
            FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;status.Text="Palauk operacijos pabaigos arba spausk „Atšaukti“.";}else if(!DiscardOrder())e.Cancel=true;else StopAudio();};
            FormClosed+=delegate{playbackTimer.Stop();playbackTimer.Dispose();audio.Dispose();batteryTip.Dispose();palette.Dispose();if(cover.Image!=null)cover.Image.Dispose();};
            SetTheme(Theme.Dark,false);SwitchPage(false);UpdateButtons();
        }
        static TableLayoutPanel Grid(int cols,int rows){return new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=cols,RowCount=rows,Margin=Padding.Empty};}
        static Label Caption(string text,int size,bool bold){return new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),Tag=bold?"title":null};}
        void Style(UiButton b,string text,int width){b.Text=text;b.Width=width;b.Height=36;b.Restyle();}
        void BuildSidebar(TableLayoutPanel shell)
        {
            var side=Grid(1,8);side.Tag="side";side.Padding=new Padding(18,24,18,18);
            side.RowStyles.Add(new RowStyle(SizeType.Absolute,80));side.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute,50));side.RowStyles.Add(new RowStyle(SizeType.Absolute,50));
            side.RowStyles.Add(new RowStyle(SizeType.Percent,100));side.RowStyles.Add(new RowStyle(SizeType.Absolute,186));
            side.RowStyles.Add(new RowStyle(SizeType.Absolute,45));side.RowStyles.Add(new RowStyle(SizeType.Absolute,27));shell.Controls.Add(side,0,0);
            var brand=Grid(2,2);brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            brand.RowStyles.Add(new RowStyle(SizeType.Absolute,34));brand.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
            var logo=new PictureBox{Image=Art.Logo(96),SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(40,40),Margin=new Padding(0,3,8,0)};brand.Controls.Add(logo,0,0);brand.SetRowSpan(logo,2);
            brand.Controls.Add(Caption("ShuffleDrop",15,true),1,0);brand.Controls.Add(Caption("Tavo muzika. Paprastai.",8,false),1,1);side.Controls.Add(brand,0,0);
            side.Controls.Add(Caption("MUZIKA",8,false),0,1);
            Style(navLibrary,"♫   Biblioteka",182);navLibrary.Navigation=true;navLibrary.Height=42;side.Controls.Add(navLibrary,0,2);
            Style(navQueue,"＋   Pridėti muziką",182);navQueue.Navigation=true;navQueue.Height=42;side.Controls.Add(navQueue,0,3);
            navLibrary.Click+=delegate{SwitchPage(false);};navQueue.Click+=delegate{SwitchPage(true);};
            var device=Grid(1,5);device.RowStyles.Add(new RowStyle(SizeType.Absolute,28));device.RowStyles.Add(new RowStyle(SizeType.Absolute,36));device.RowStyles.Add(new RowStyle(SizeType.Absolute,32));device.RowStyles.Add(new RowStyle(SizeType.Absolute,39));device.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            device.Controls.Add(Caption("PRIJUNGTAS ĮRENGINYS",8,false),0,0);
            devices.DropDownStyle=ComboBoxStyle.DropDownList;devices.Dock=DockStyle.Top;devices.Margin=Padding.Empty;device.Controls.Add(devices,0,1);
            deviceInfo.Dock=DockStyle.Fill;deviceInfo.TextAlign=ContentAlignment.MiddleLeft;device.Controls.Add(deviceInfo,0,2);
            batteryInfo.Dock=DockStyle.Fill;batteryInfo.Font=new Font("Segoe UI",8.5f);batteryInfo.TextAlign=ContentAlignment.MiddleLeft;batteryInfo.AccessibleName="Baterijos procento nuskaitymas nepalaikomas";batteryInfo.LinkClicked+=delegate{ShowBatteryHelp();};device.Controls.Add(batteryInfo,0,3);
            Style(refresh,"↻  Atnaujinti",180);device.Controls.Add(refresh,0,4);side.Controls.Add(device,0,5);
            side.Controls.Add(themeToggle,0,6);var help=new LinkLabel{Text="Pagalba  ·  v0.3",AutoSize=true,Tag="help",Margin=new Padding(0,5,0,0)};help.LinkClicked+=delegate{Help();};side.Controls.Add(help,0,7);
        }
        void BuildContent(TableLayoutPanel shell)
        {
            var main=Grid(1,3);main.Padding=new Padding(28,25,28,12);main.RowStyles.Add(new RowStyle(SizeType.Absolute,91));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));main.RowStyles.Add(new RowStyle(SizeType.Absolute,47));shell.Controls.Add(main,1,0);
            var heading=Grid(1,3);heading.RowStyles.Add(new RowStyle(SizeType.Absolute,41));heading.RowStyles.Add(new RowStyle(SizeType.Absolute,24));heading.RowStyles.Add(new RowStyle(SizeType.Absolute,22));
            pageTitle.Text="Tavo biblioteka";pageTitle.Font=new Font("Segoe UI",25,FontStyle.Bold);pageTitle.Tag="title";pageTitle.AutoSize=true;heading.Controls.Add(pageTitle,0,0);
            pageHint.Text="Muzika, kuri keliauja kartu su tavimi.";pageHint.AutoSize=true;heading.Controls.Add(pageHint,0,1);
            totals.Dock=DockStyle.Fill;totals.Font=new Font("Segoe UI",9);heading.Controls.Add(totals,0,2);main.Controls.Add(heading,0,0);
            tabs.Dock=DockStyle.Fill;libraryTab.Dock=queueTab.Dock=DockStyle.Fill;libraryTab.Padding=queueTab.Padding=Padding.Empty;tabs.Controls.AddRange(new Control[]{libraryTab,queueTab});main.Controls.Add(tabs,0,1);BuildLibrary();BuildQueue();
            var footer=Grid(2,2);footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));footer.RowStyles.Add(new RowStyle(SizeType.Absolute,3));
            status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.Font=new Font("Segoe UI",9);status.AutoEllipsis=true;status.Text="Prijunk iPod ir atnaujink biblioteką.";footer.Controls.Add(status,0,0);
            Style(cancel,"Atšaukti",100);cancel.Visible=false;footer.Controls.Add(cancel,1,0);progress.Dock=DockStyle.Fill;progress.Margin=Padding.Empty;footer.Controls.Add(progress,0,1);footer.SetColumnSpan(progress,2);main.Controls.Add(footer,0,2);
        }
        void SetupList(MusicList view)
        {view.Dock=DockStyle.Fill;view.View=View.Details;view.FullRowSelect=true;view.HideSelection=false;view.BorderStyle=BorderStyle.None;view.ShowItemToolTips=true;view.Font=new Font("Segoe UI",10);view.Margin=Padding.Empty;}
        void BuildLibrary()
        {
            var panel=Grid(1,4);panel.RowStyles.Add(new RowStyle(SizeType.Absolute,50));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,48));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,30));libraryTab.Controls.Add(panel);
            var find=Grid(3,1);find.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65));find.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));find.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,176));
            find.Controls.Add(new Label{Text="Paieška",AutoSize=true,Padding=new Padding(0,8,0,0)},0,0);search.Dock=DockStyle.Top;search.Margin=new Padding(0,0,16,0);search.AccessibleName="Ieškoti dainos, atlikėjo arba albumo";find.Controls.Add(search,1,0);
            var addMusic=new UiButton{Primary=true};Style(addMusic,"＋  Pridėti muziką",176);addMusic.Click+=delegate{SwitchPage(true);};find.Controls.Add(addMusic,2,0);panel.Controls.Add(find,0,0);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            Style(export,"↓ Į kompiuterį",137);Style(delete,"Šalinti",85);delete.Danger=true;Style(up,"↑",36);Style(down,"↓",36);Style(saveOrder,"Išsaugoti tvarką",148);Style(undo,"↶ Atkurti",113);
            actions.Controls.AddRange(new Control[]{export,delete,up,down,saveOrder,undo});panel.Controls.Add(actions,0,1);
            SetupList(library);library.Columns.Add("#",44);library.Columns.Add("DAINA",290);library.Columns.Add("ATLIKĖJAS",190);library.Columns.Add("ALBUMAS",220);library.Columns.Add("LAIKAS",76);library.Columns.Add("DYDIS",82);panel.Controls.Add(library,0,2);
            library.Resize+=delegate{int width=library.ClientSize.Width-44-76-82-18;if(width>400){library.Columns[1].Width=(int)(width*.42);library.Columns[2].Width=(int)(width*.28);library.Columns[3].Width=width-library.Columns[1].Width-library.Columns[2].Width;}};
            selectedInfo.Dock=DockStyle.Fill;selectedInfo.TextAlign=ContentAlignment.MiddleLeft;selectedInfo.Font=new Font("Segoe UI",8.5f);panel.Controls.Add(selectedInfo,0,3);
            search.TextChanged+=delegate{RenderLibrary();};library.SelectedIndexChanged+=delegate{UpdateButtons();};
            library.ColumnClick+=delegate(object s,ColumnClickEventArgs e){descending=sortColumn==e.Column&&!descending;sortColumn=e.Column;RenderLibrary();};
            library.KeyDown+=delegate(object s,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.A){foreach(ListViewItem row in library.Items)row.Selected=true;e.SuppressKeyPress=true;}if(e.KeyCode==Keys.Delete&&!busy)RemoveTracks();if(e.KeyCode==Keys.Enter){PlaySelected();e.SuppressKeyPress=true;}};
            export.Click+=delegate{ExportTracks();};delete.Click+=delegate{RemoveTracks();};up.Click+=delegate{MoveSelected(-1);};down.Click+=delegate{MoveSelected(1);};
            saveOrder.Click+=delegate{Start(new Work{Kind="order",Device=CurrentDevice,Snapshot=snapshot,Order=ordered.Select(t=>t.Relative).ToArray()});};
            undo.Click+=delegate{if(!DiscardOrder()||CurrentDevice==null)return;if(MessageBox.Show(this,"Atkurti būseną prieš paskutinį pakeitimą?\n\nPašalintoms dainoms grąžinti reikalingos šiame kompiuteryje išsaugotos kopijos.","Atkurti biblioteką",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)Start(new Work{Kind="restore",Device=CurrentDevice});};
        }
        void BuildQueue()
        {
            var panel=Grid(1,4);panel.RowStyles.Add(new RowStyle(SizeType.Absolute,48));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,46));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,30));queueTab.Controls.Add(panel);
            var hint=Caption("Pasirink MP3 failus arba nutempk muzikos aplanką į sąrašą.",10,false);hint.Padding=new Padding(0,8,0,0);panel.Controls.Add(hint,0,0);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};Style(add,"＋ Failai",108);Style(folder,"＋ Aplankas",132);Style(dropQueue,"Išimti iš eilės",134);send.Primary=true;Style(send,"Perkelti į iPod →",178);actions.Controls.AddRange(new Control[]{add,folder,dropQueue,send});panel.Controls.Add(actions,0,1);
            SetupList(queue);queue.Columns.Add("DAINA / FAILAS",690);queue.Columns.Add("DYDIS",110);queue.AllowDrop=true;queue.Resize+=delegate{queue.Columns[0].Width=Math.Max(300,queue.ClientSize.Width-130);};panel.Controls.Add(queue,0,2);
            queueSummary.Dock=DockStyle.Fill;queueSummary.TextAlign=ContentAlignment.MiddleLeft;queueSummary.Font=new Font("Segoe UI",9);panel.Controls.Add(queueSummary,0,3);
            add.Click+=delegate{using(var d=new OpenFileDialog{Filter="MP3 dainos|*.mp3",Multiselect=true})if(d.ShowDialog(this)==DialogResult.OK)AddPaths(d.FileNames);};
            folder.Click+=delegate{using(var d=new FolderBrowserDialog{Description="Pasirink muzikos aplanką",ShowNewFolderButton=false})if(d.ShowDialog(this)==DialogResult.OK)AddPaths(new[]{d.SelectedPath});};
            dropQueue.Click+=delegate{foreach(ListViewItem row in queue.SelectedItems)files.Remove((string)row.Tag);RenderQueue();};
            queue.DragEnter+=delegate(object s,DragEventArgs e){if(!busy&&e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};queue.DragDrop+=delegate(object s,DragEventArgs e){if(!busy)AddPaths((string[])e.Data.GetData(DataFormats.FileDrop));};
            send.Click+=delegate{Start(new Work{Kind="add",Device=CurrentDevice,Files=files.ToArray()});};RenderQueue();
        }
        void BuildPlayer(TableLayoutPanel shell)
        {
            var player=Grid(3,1);player.Tag="surface";player.Padding=new Padding(24,15,24,14);player.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,330));player.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));player.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165));shell.Controls.Add(player,0,1);shell.SetColumnSpan(player,2);
            var now=Grid(2,2);now.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));now.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));now.RowStyles.Add(new RowStyle(SizeType.Percent,50));now.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            cover.Image=Art.Logo(128);cover.SizeMode=PictureBoxSizeMode.Zoom;cover.Size=new Size(64,64);cover.Margin=new Padding(0,14,14,0);now.Controls.Add(cover,0,0);now.SetRowSpan(cover,2);
            playing.Text="Pasirink dainą";playing.Font=new Font("Segoe UI",11,FontStyle.Bold);playing.Tag="title";playing.AutoEllipsis=true;playing.Dock=DockStyle.Fill;playing.TextAlign=ContentAlignment.BottomLeft;playing.Padding=new Padding(0,0,8,4);now.Controls.Add(playing,1,0);
            artistPlaying.Text="Klausyk per kompiuterį";artistPlaying.AutoEllipsis=true;artistPlaying.Dock=DockStyle.Fill;artistPlaying.Padding=new Padding(0,3,8,0);now.Controls.Add(artistPlaying,1,1);player.Controls.Add(now,0,0);
            var transport=Grid(1,3);transport.RowStyles.Add(new RowStyle(SizeType.Absolute,43));transport.RowStyles.Add(new RowStyle(SizeType.Absolute,25));transport.RowStyles.Add(new RowStyle(SizeType.Percent,100));transport.Padding=new Padding(14,0,24,0);
            var buttons=new FlowLayoutPanel{AutoSize=true,Anchor=AnchorStyles.None,WrapContents=false,Margin=Padding.Empty};play.Primary=true;Style(play,"▶ Groti",105);Style(pause,"Ⅱ Pauzė",108);Style(stop,"■ Stabdyti",111);buttons.Controls.AddRange(new Control[]{play,pause,stop});transport.Controls.Add(buttons,0,0);
            seek.Dock=DockStyle.Fill;seek.Enabled=false;seek.SeekRequested+=delegate(double fraction){try{audio.Seek(fraction);UpdatePlayback();}catch(Exception e){StopAudio();ShowError(e.Message);}};transport.Controls.Add(seek,0,1);
            elapsed.Dock=DockStyle.Fill;elapsed.TextAlign=ContentAlignment.TopCenter;elapsed.Font=new Font("Segoe UI",8.5f);elapsed.Text="0:00 / 0:00";transport.Controls.Add(elapsed,0,2);player.Controls.Add(transport,1,0);
            var sound=Grid(1,3);sound.Padding=new Padding(10,16,0,0);sound.RowStyles.Add(new RowStyle(SizeType.Absolute,28));sound.RowStyles.Add(new RowStyle(SizeType.Absolute,30));sound.RowStyles.Add(new RowStyle(SizeType.Percent,100));volumeLabel.Text="Garsumas 65 %";volumeLabel.AutoSize=true;sound.Controls.Add(volumeLabel,0,0);volume.Fraction=.65;volume.Dock=DockStyle.Fill;volume.AccessibleName="Garsumas";volume.SeekRequested+=delegate(double fraction){volume.Fraction=fraction;audio.Volume=(float)volume.Fraction;volumeLabel.Text="Garsumas "+Math.Round(volume.Fraction*100)+" %";};sound.Controls.Add(volume,0,1);player.Controls.Add(sound,2,0);
        }
        void SwitchPage(bool adding)
        {
            tabs.SelectedTab=adding?queueTab:libraryTab;navLibrary.Active=!adding;navQueue.Active=adding;navLibrary.Restyle();navQueue.Restyle();
            pageTitle.Text=adding?"Pridėti muziką":"Tavo biblioteka";pageHint.Text=adding?"Iš kompiuterio tiesiai į tavo iPod.":"Muzika, kuri keliauja kartu su tavimi.";
        }
        internal void SetTheme(bool dark,bool persist)
        {
            applyingTheme=true;Theme.Dark=dark;themeToggle.Checked=dark;
            palette.GlobalPaletteMode=dark?PaletteMode.Microsoft365Black:PaletteMode.Microsoft365White;
            ApplyColors(this,Theme.Background);applyingTheme=false;Invalidate(true);
            SetTitleTheme();
            if(persist)try{Theme.Save();}catch(Exception e){status.Text="Nepavyko išsaugoti temos: "+e.Message;}
        }
        void SetTitleTheme()
        { if(IsHandleCreated)try{int dark=Theme.Dark?1:0;DwmSetWindowAttribute(Handle,20,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){} }
        void ApplyColors(Control c,Color inherited)
        {
            Color back=Equals(c.Tag,"side")?Theme.Side:Equals(c.Tag,"surface")?Theme.Surface:inherited;c.BackColor=back;c.ForeColor=Theme.Text;
            var button=c as UiButton;if(button!=null)button.Restyle();
            var label=c as Label;if(label!=null)label.ForeColor=Equals(c.Tag,"title")?Theme.Text:Theme.Muted;
            var link=c as LinkLabel;if(link!=null){link.LinkColor=Theme.Muted;link.ActiveLinkColor=Theme.Accent;}
            var text=c as KryptonTextBox;if(text!=null){text.StateCommon.Back.Color1=Theme.Surface;text.StateCommon.Content.Color1=Theme.Text;text.StateCommon.Content.Padding=new Padding(9,7,9,7);text.StateCommon.Border.Color1=Theme.Border;text.StateCommon.Border.Rounding=6;}
            var combo=c as KryptonComboBox;if(combo!=null){combo.StateCommon.ComboBox.Back.Color1=Theme.Surface;combo.StateCommon.ComboBox.Content.Color1=Theme.Text;combo.StateCommon.ComboBox.Border.Color1=Theme.Border;combo.StateCommon.ComboBox.Border.Rounding=5;}
            foreach(Control child in c.Controls)ApplyColors(child,back);
            var list=c as MusicList;if(list!=null)list.Restyle();
        }
        static string TimeText(TimeSpan time){return ((int)time.TotalMinutes)+":"+time.Seconds.ToString("00");}
        void UpdatePlayback()
        {
            try
            {
                var error=audio.TakeError();if(error!=null){StopAudio();status.Text="Garso atkūrimas nutrūko: "+error.Message;}
                seek.Enabled=audio.Opened&&!busy;seek.Fraction=audio.Duration.TotalSeconds>0?audio.Position.TotalSeconds/audio.Duration.TotalSeconds:0;
                elapsed.Text=TimeText(audio.Position)+" / "+TimeText(audio.Duration);pause.Text=audio.Playing?"Ⅱ Pauzė":"▶ Tęsti";
                pause.Enabled=stop.Enabled=audio.Opened&&!busy;
            }
            catch(Exception e){StopAudio();status.Text="Garso išvestis nepasiekiama: "+e.Message;}
        }
        void LoadCover(string path)
        {
            Image image=null;
            try{using(var tag=TagLib.File.Create(path)){var picture=tag.Tag.Pictures.FirstOrDefault();if(picture!=null&&picture.Data.Count<5*1024*1024)using(var memory=new MemoryStream(picture.Data.Data))using(var source=Image.FromStream(memory))image=new Bitmap(source);}}
            catch(Exception){ }
            var old=cover.Image;cover.Image=image??Art.Logo(128);if(old!=null)old.Dispose();
        }
    }
}
