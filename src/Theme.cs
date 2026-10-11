using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Krypton.Toolkit;

namespace ShuffleDrop
{
    internal static class Theme
    {
        public static bool Dark = true;
        public static Color Background { get { return Dark ? Color.FromArgb(19,23,29) : Color.FromArgb(245,247,249); } }
        public static Color Surface { get { return Dark ? Color.FromArgb(25,30,38) : Color.White; } }
        public static Color Side { get { return Dark ? Color.FromArgb(15,19,25) : Color.FromArgb(235,240,243); } }
        public static Color Text { get { return Dark ? Color.FromArgb(234,240,244) : Color.FromArgb(27,42,52); } }
        public static Color Muted { get { return Dark ? Color.FromArgb(155,171,185) : Color.FromArgb(93,110,124); } }
        public static Color Border { get { return Dark ? Color.FromArgb(43,53,64) : Color.FromArgb(220,228,233); } }
        public static Color Accent { get { return Dark ? Color.FromArgb(66,213,177) : Color.FromArgb(0,120,99); } }
        public static Color Selected { get { return Dark ? Color.FromArgb(33,68,67) : Color.FromArgb(219,243,234); } }
        public static Color Hover { get { return Dark ? Color.FromArgb(36,44,54) : Color.FromArgb(234,241,244); } }
        public static Color Danger { get { return Dark ? Color.FromArgb(248,141,144) : Color.FromArgb(174,50,60); } }
        public static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ShuffleDrop","theme.txt"); } }
        public static void Load() { try { Dark = !File.Exists(SettingsPath) || File.ReadAllText(SettingsPath).Trim() != "light"; } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        public static void Save() { Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)); File.WriteAllText(SettingsPath,Dark?"dark":"light"); }
    }
    internal sealed class UiButton : KryptonButton
    {
        public bool Primary, Danger, Navigation, Active;
        public UiButton() { Height=36; Cursor=Cursors.Hand; Margin=new Padding(0,0,8,0); }
        public void Restyle()
        {
            Color back = Primary ? Theme.Accent : Active ? Theme.Selected : Navigation ? Theme.Side : Theme.Surface;
            Color fore = Primary ? (Theme.Dark ? Theme.Side : Color.White) : Danger ? Theme.Danger : Active ? Theme.Accent : Theme.Text;
            StateCommon.Back.Color1=StateCommon.Back.Color2=back;
            StateCommon.Back.ColorStyle=PaletteColorStyle.Solid;
            OverrideDefault.Back.Color1=OverrideDefault.Back.Color2=back;
            OverrideDefault.Border.Color1=Theme.Border;
            OverrideFocus.Back.Color1=OverrideFocus.Back.Color2=back;
            OverrideFocus.Border.Color1=Theme.Accent;
            StateCommon.Border.Color1=StateCommon.Border.Color2=Navigation?back:Theme.Border;
            StateCommon.Border.Rounding=6; StateCommon.Border.Width=1;
            StateCommon.Content.ShortText.Color1=fore; StateCommon.Content.ShortText.Font=new Font("Segoe UI",10,Primary?FontStyle.Bold:FontStyle.Regular);
            StateTracking.Back.Color1=StateTracking.Back.Color2=Primary?ControlPaint.Light(Theme.Accent):Theme.Hover;
            StatePressed.Back.Color1=StatePressed.Back.Color2=Theme.Selected;
            StateDisabled.Back.Color1=StateDisabled.Back.Color2=Theme.Surface;
            StateDisabled.Content.ShortText.Color1=Theme.Muted;
            StateDisabled.Border.Color1=Theme.Border;
            if(Navigation) StateCommon.Content.ShortText.TextH=PaletteRelativeAlign.Near;
            Invalidate();
        }
    }
    internal sealed class WorkProgress : Control
    {
        int value;
        public int Value {get{return value;}set{this.value=Math.Max(0,Math.Min(100,value));Invalidate();}}
        public WorkProgress(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Theme.Border);using(var b=new SolidBrush(Theme.Accent))e.Graphics.FillRectangle(b,0,0,Width*value/100,Height);}
    }
    internal sealed class ContentPage : Panel
    { public ContentPage(string text) { Text=text; } }
    internal sealed class PageHost : Panel
    {
        ContentPage selected;
        public ContentPage SelectedTab { get { return selected; } set { selected=value; foreach(Control c in Controls)c.Visible=ReferenceEquals(c,value); if(value!=null)value.BringToFront(); } }
        public int SelectedIndex { get { return selected==null?-1:Controls.IndexOf(selected); } set { SelectedTab=(ContentPage)Controls[value]; } }
    }
    internal sealed class ThemeToggle : CheckBox
    {
        public ThemeToggle() { AutoSize=false; Size=new Size(170,36); Cursor=Cursors.Hand; Text="Tamsi tema"; SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Art.Round(new Rectangle(124,8,38,21),10))using(var b=new SolidBrush(Checked?Theme.Accent:Theme.Border))e.Graphics.FillPath(b,path);
            using(var b=new SolidBrush(Checked?Theme.Side:Color.White))e.Graphics.FillEllipse(b,Checked?144:127,11,15,15);
            TextRenderer.DrawText(e.Graphics,Checked?"Tamsi tema":"Šviesi tema",Font,new Rectangle(0,0,120,36),Theme.Text,TextFormatFlags.VerticalCenter);
            if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
        }
    }
    internal sealed class SeekBar : Control
    {
        double fraction;
        public double Fraction { get { return fraction; } set { fraction=Math.Max(0,Math.Min(1,value)); Invalidate(); } }
        public event Action<double> SeekRequested;
        public SeekBar() { Height=24; Cursor=Cursors.Hand; SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); AccessibleName="Dainos eiga"; TabStop=true; }
        protected override void OnPaint(PaintEventArgs e)
        { e.Graphics.Clear(BackColor); using(var b=new SolidBrush(Theme.Border))e.Graphics.FillRectangle(b,0,Height/2-2,Width,4); using(var b=new SolidBrush(Theme.Accent)){e.Graphics.FillRectangle(b,0,Height/2-2,(int)(Width*fraction),4); if(Enabled)e.Graphics.FillEllipse(b,Math.Max(0,(int)((Width-10)*fraction)),Height/2-5,10,10);} }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); if(Enabled && SeekRequested!=null)SeekRequested((double)e.X/Math.Max(1,Width)); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if(SeekRequested!=null && (e.KeyCode==Keys.Left||e.KeyCode==Keys.Right)){SeekRequested(Math.Max(0,Math.Min(1,Fraction+(e.KeyCode==Keys.Right?0.02:-0.02))));e.Handled=true;} }
        protected override bool IsInputKey(Keys keyData) { return keyData==Keys.Left||keyData==Keys.Right||base.IsInputKey(keyData); }
    }
    internal sealed class MusicList : ListView
    {
        readonly ImageList heights=new ImageList();
        public MusicList()
        {
            DoubleBuffered=true; OwnerDraw=true; heights.ImageSize=new Size(1,38); SmallImageList=heights;
            DrawColumnHeader+=delegate(object s,DrawListViewColumnHeaderEventArgs e){ using(var b=new SolidBrush(Theme.Surface))e.Graphics.FillRectangle(b,e.Bounds); TextRenderer.DrawText(e.Graphics,e.Header.Text,new Font("Segoe UI",9,FontStyle.Bold),Rectangle.Inflate(e.Bounds,-10,0),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis); using(var pen=new Pen(Theme.Border))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1); };
            DrawItem+=delegate(object s,DrawListViewItemEventArgs e) { };
            DrawSubItem+=delegate(object s,DrawListViewSubItemEventArgs e)
            {
                Color back=e.Item.Selected?Theme.Selected:Theme.Surface;
                using(var b=new SolidBrush(back))e.Graphics.FillRectangle(b,e.Bounds);
                Color fore=e.ColumnIndex==1?Theme.Text:Theme.Muted;
                var track=e.Item.Tag as LibraryTrack; if(track!=null && track.Missing)fore=Theme.Danger;
                if(e.Item.Selected && e.ColumnIndex==0)using(var b=new SolidBrush(Theme.Accent))e.Graphics.FillRectangle(b,e.Bounds.Left,e.Bounds.Top+7,3,e.Bounds.Height-14);
                TextRenderer.DrawText(e.Graphics,e.SubItem.Text,Font,Rectangle.Inflate(e.Bounds,-10,0),fore,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            };
        }
        public void Restyle(){BackColor=Theme.Surface;ForeColor=Theme.Text;Invalidate();}
        protected override void Dispose(bool disposing){if(disposing)heights.Dispose();base.Dispose(disposing);}
    }
    internal static class Art
    {
        public static GraphicsPath Round(Rectangle r,int radius)
        { var p=new GraphicsPath(); int d=radius*2; p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p; }
        public static Bitmap Logo(int size)
        {
            var b=new Bitmap(size,size); using(var g=Graphics.FromImage(b))
            {
                g.SmoothingMode=SmoothingMode.AntiAlias; g.ScaleTransform(size/256f,size/256f);
                using(var p=Round(new Rectangle(8,8,240,240),54))using(var brush=new LinearGradientBrush(new Point(20,20),new Point(230,230),Color.FromArgb(61,218,180),Color.FromArgb(0,111,113)))g.FillPath(brush,p);
                using(var pen=new Pen(Color.FromArgb(230,255,255),17))g.DrawEllipse(pen,59,59,138,138);
                using(var brush=new SolidBrush(Color.FromArgb(245,255,252)))g.FillPolygon(brush,new[]{new Point(111,93),new Point(111,164),new Point(163,128)});
                using(var pen=new Pen(Color.FromArgb(190,255,244),7)){g.DrawLine(pen,110,30,145,30);g.DrawLine(pen,213,118,213,138);}
            }
            return b;
        }
        public static void SaveIcon(string path)
        {
            int[] sizes={16,24,32,48,64,128,256}; var images=new System.Collections.Generic.List<byte[]>();
            foreach(int size in sizes)using(var b=Logo(size))using(var m=new MemoryStream())using(var w=new BinaryWriter(m))
            {
                int maskStride=((size+31)/32)*4;
                w.Write(40);w.Write(size);w.Write(size*2);w.Write((short)1);w.Write((short)32);w.Write(0);w.Write(size*size*4+maskStride*size);w.Write(0);w.Write(0);w.Write(0);w.Write(0);
                for(int y=size-1;y>=0;y--)for(int x=0;x<size;x++){Color c=b.GetPixel(x,y);w.Write(c.B);w.Write(c.G);w.Write(c.R);w.Write(c.A);}
                w.Write(new byte[maskStride*size]);w.Flush();images.Add(m.ToArray());
            }
            using(var stream=File.Create(path))using(var writer=new BinaryWriter(stream))
            {writer.Write((short)0);writer.Write((short)1);writer.Write((short)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((short)1);writer.Write((short)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}foreach(var bytes in images)writer.Write(bytes);}
        }
    }
}
