using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

public class Encoder {
 public volatile bool Cancel;
 string bin = AppDomain.CurrentDomain.BaseDirectory;
 public static string Q(string s) { return "\""+s+"\""; }
 public string Run(string exe,string args,Action<string> progress) {
  if(Cancel) throw new OperationCanceledException();
  var info=new ProcessStartInfo(Path.Combine(bin,exe),args) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardErrorEncoding=Encoding.UTF8 };
  var errors=new StringBuilder(); var output=new StringBuilder();
  using(var p=new Process { StartInfo=info }) {
   p.OutputDataReceived+=(s,e)=>{if(e.Data!=null) { output.AppendLine(e.Data); if(progress!=null) progress(e.Data); }};
   p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null) { lock(errors) { errors.AppendLine(e.Data); if(errors.Length>12000) errors.Remove(0,6000); } }};
   p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
   while(!p.WaitForExit(150)) { if(Cancel) { try {p.Kill();} catch {} p.WaitForExit(); throw new OperationCanceledException(); } }
   p.WaitForExit(); if(Cancel) throw new OperationCanceledException();
   if(p.ExitCode!=0) throw new Exception(errors.ToString());
  } return output.ToString().Trim();
 }
 public double Duration(string path) {
  double d; string v=Run("ffprobe.exe","-v error -show_entries format=duration -of default=nw=1:nk=1 "+Q(path),null);
  if(!double.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out d)||double.IsNaN(d)||double.IsInfinity(d)||d<=0) throw new Exception("无法读取视频时长。"); return d;
 }
 public string Compress(string input,string folder,long limit,Action<int,string> report) {
  Directory.CreateDirectory(folder); double duration=Duration(input);
  string temp=Path.Combine(folder,".compress-"+Guid.NewGuid().ToString("N")+".mp4");
  double total=limit*8.0/duration/1000*0.90; int audio=total<250?48:96;
  int rate=(int)Math.Min(2200,Math.Floor(total-audio));
  if(rate<40) throw new Exception("视频过长，无法在当前大小限制内保持可用画质。");
  try {
   for(int attempt=0;attempt<4;attempt++) {
    int side=rate<350?480:720;
    string filter="scale=w='if(gte(iw,ih),min(iw,"+(side*16/9)+"),min(iw,"+side+"))':h='if(gte(iw,ih),min(ih,"+side+"),min(ih,"+(side*16/9)+"))':force_original_aspect_ratio=decrease:force_divisible_by=2,setsar=1";
    report(0,attempt==0?"压缩中":"调整大小，第 "+(attempt+1)+" 次压缩");
    string args="-hide_banner -loglevel error -nostdin -y -i "+Q(input)+" -map 0:V:0 -map 0:a:0? -sn -dn -map_metadata -1 -map_chapters -1 -vf "+Q(filter)+" -r 30 -c:v libx264 -preset fast -b:v "+rate+"k -maxrate "+rate+"k -bufsize "+(rate*2)+"k -pix_fmt yuv420p -c:a aac -b:a "+audio+"k -ac 2 -movflags +faststart -progress pipe:1 -nostats "+Q(temp);
    Run("ffmpeg.exe",args,line=> { if(line.StartsWith("out_time_us=")) { double us; if(double.TryParse(line.Substring(12),out us)) report((int)Math.Max(0,Math.Min(99,us/1000000/duration*100)),"压缩中"); }});
    long size=new FileInfo(temp).Length;
    if(size>0 && size<limit) {
     double actual=Duration(temp); if(Math.Abs(actual-duration)>Math.Max(2,duration*0.01)) throw new Exception("输出时长校验失败，未保存为完成文件。");
     string name=Path.GetFileNameWithoutExtension(input)+"_手机版"; string dest=Path.Combine(folder,name+".mp4"); int n=2;
     while(File.Exists(dest)) dest=Path.Combine(folder,name+"_"+(n++)+".mp4");
     if(Cancel) throw new OperationCanceledException(); File.Move(temp,dest); report(100,"完成"); return dest;
    }
    rate=(int)(rate*Math.Min(0.8,limit*0.85/size)); if(rate<40) break;
   } throw new Exception("未能压缩到限制以内，请选择更短的视频。");
  } finally { if(File.Exists(temp)) File.Delete(temp); }
 }
}
public class MainForm:Form {
 ListView list=new ListView(); Label status=new Label(); TextBox output=new TextBox(); ProgressBar bar=new ProgressBar();
 Button add,clear,choose,start,stop; Encoder encoder; bool busy;
 public MainForm() {
  Text="手机视频压缩 · 180MB"; ClientSize=new Size(880,570); MinimumSize=new Size(800,560); Font=new Font("Microsoft YaHei UI",10); StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(246,248,251);
  var title=new Label { Text="批量压缩，轻松发送",Font=new Font(Font.FontFamily,20,FontStyle.Bold),Location=new Point(24,20),AutoSize=true }; Controls.Add(title);
  Controls.Add(new Label { Text="手机画质 · 每个文件小于 180MB · 保留原视频 · 全程本地处理",Location=new Point(26,65),AutoSize=true });
  add=ButtonAt("添加视频",24,100,Add); clear=ButtonAt("清空列表",152,100,(s,e)=>list.Items.Clear());
  list.Bounds=new Rectangle(24,148,832,270); list.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; list.View=View.Details; list.FullRowSelect=true; list.GridLines=false;
  list.Columns.Add("视频",400); list.Columns.Add("原大小",115); list.Columns.Add("状态 / 输出大小",280); Controls.Add(list);
  Controls.Add(new Label {Text="保存到",Location=new Point(24,435),AutoSize=true,Anchor=AnchorStyles.Bottom|AnchorStyles.Left});
  output.Bounds=new Rectangle(90,432,630,28); output.ReadOnly=true; output.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
  output.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),"手机压缩视频"); Controls.Add(output);
  choose=ButtonAt("选择位置",732,430,(s,e)=>{using(var d=new FolderBrowserDialog()) {if(d.ShowDialog()==DialogResult.OK) output.Text=d.SelectedPath;}}); choose.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
  start=ButtonAt("开始压缩",24,480,Start); start.BackColor=Color.FromArgb(39,103,220); start.ForeColor=Color.White; start.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
  stop=ButtonAt("停止",152,480,(s,e)=>{encoder.Cancel=true; stop.Enabled=false; status.Text="正在停止，保留已完成的视频…";}); stop.Enabled=false; stop.Anchor=start.Anchor;
  var open=ButtonAt("打开结果",280,480,(s,e)=>{if(Directory.Exists(output.Text)) Process.Start("explorer.exe",Encoder.Q(output.Text));}); open.Anchor=start.Anchor;
  bar.Bounds=new Rectangle(428,488,428,16); bar.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; Controls.Add(bar);
  status.Bounds=new Rectangle(24,529,832,24); status.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; status.Text="添加多个视频后，点击开始压缩。"; Controls.Add(status);
  FormClosing+=(s,e)=>{if(busy) {e.Cancel=true; encoder.Cancel=true; status.Text="正在停止，请稍后关闭窗口。";}};
 }
 Button ButtonAt(string text,int x,int y,EventHandler action) {var b=new Button {Text=text,Location=new Point(x,y),Size=new Size(116,34),FlatStyle=FlatStyle.Flat}; b.Click+=action; Controls.Add(b); return b;}
 void Add(object s,EventArgs e) {using(var d=new OpenFileDialog {Multiselect=true,Filter="视频文件|*.mp4;*.mov;*.mkv;*.avi;*.m4v;*.wmv;*.webm;*.mts;*.m2ts;*.3gp|所有文件|*.*"}) {if(d.ShowDialog()!=DialogResult.OK)return; foreach(string file in d.FileNames) {bool exists=false; foreach(ListViewItem i in list.Items) if(string.Equals((string)i.Tag,file,StringComparison.OrdinalIgnoreCase)) exists=true; if(exists)continue; var item=new ListViewItem(Path.GetFileName(file)); item.Tag=file; item.SubItems.Add((new FileInfo(file).Length/1000000.0).ToString("F1")+" MB"); item.SubItems.Add("等待"); list.Items.Add(item); } }}
 void UI(Action a) {if(!IsDisposed) BeginInvoke(a);}
 async void Start(object s,EventArgs e) {
  if(list.Items.Count==0) {MessageBox.Show("请先添加视频。");return;}
  encoder=new Encoder(); busy=true; add.Enabled=clear.Enabled=choose.Enabled=start.Enabled=false; stop.Enabled=true;
  string folder=output.Text; int ok=0,failed=0; var items=new ListViewItem[list.Items.Count]; list.Items.CopyTo(items,0);
  await Task.Run(()=> {foreach(var item in items) {
   if(encoder.Cancel)break;
   try {string result=encoder.Compress((string)item.Tag,folder,180000000,(p,msg)=>UI(()=>{bar.Value=p;item.SubItems[2].Text=msg+" "+p+"%";status.Text=item.Text+" · "+msg;})); ok++; UI(()=>item.SubItems[2].Text="完成 · "+(new FileInfo(result).Length/1000000.0).ToString("F1")+" MB");}
   catch(OperationCanceledException) {UI(()=>item.SubItems[2].Text="已停止");break;}
   catch(Exception ex) {failed++; string error=ex.Message; UI(()=>{item.SubItems[2].Text="失败（双击查看原因）";item.ToolTipText=error;});}
  }});
  busy=false;add.Enabled=clear.Enabled=choose.Enabled=start.Enabled=true;stop.Enabled=false;bar.Value=0;status.Text=string.Format("{0}：成功 {1} 个，失败 {2} 个。结果已另存，原视频保留。",encoder.Cancel?"已停止":"处理结束",ok,failed);
 }
 protected override void OnLoad(EventArgs e) {base.OnLoad(e);list.DoubleClick+=(s,a)=>{if(list.SelectedItems.Count>0&&!string.IsNullOrEmpty(list.SelectedItems[0].ToolTipText))MessageBox.Show(list.SelectedItems[0].ToolTipText,"错误详情");};}
 [STAThread] public static void Main(string[] args) {
  if(args.Length>=3&&args[0]=="--test") {try {var enc=new Encoder();long limit=args.Length>3?long.Parse(args[3]):180000000;var result=enc.Compress(args[1],args[2],limit,(p,m)=>{});File.WriteAllText(Path.Combine(args[2],"test-result.txt"),result); } catch(Exception e) {File.WriteAllText(Path.Combine(args[2],"test-error.txt"),e.ToString());Environment.ExitCode=1;}return;}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm());
 }
}
