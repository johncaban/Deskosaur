using System; using System.Collections.Generic; using System.IO;
namespace Deskosaur {
public static class GifMain {
  static void Raw(Surface s, BinaryWriter w){ foreach(int c in s.P){ w.Write((byte)(c>>16)); w.Write((byte)(c>>8)); w.Write((byte)c);} }
  public static void Main(string[] a){
    if(a[0]=="launcher"){
      SaveData sd=new SaveData(); sd.Unlocked=7; Launcher l=new Launcher(sd); Input inp=new Input();
      using(var w=new BinaryWriter(File.Create("out/launcher.raw"))){
        for(int f=0;f<560;f++){ inp.Latch(); l.Tick(inp); l.Render(inp); if(f>=320 && f%2==0) Raw(l.Fb,w);} }
      return; }
    int lv=int.Parse(a[1]);
    // solve with the bot (no desktop), then replay its inputs through the real game
    World bw=new World(Levels.All[lv],640,360,360); bw.BotMode=true; bw.UseDesk=false;
    int exp,frames; Bot.Solve(bw,Phys.Spawn(bw.SpawnX,bw.SpawnY),bw.ExitX,bw.ExitY,true,1500000,out exp,out frames);
    List<byte> acts=new List<byte>(Bot.LastActions);
    int W=640,H=376; SaveData s2=new SaveData(); s2.Look.Hat=1; s2.Look.Glasses=1; s2.Look.Shoes=5;
    Game g=new Game(W,H,360,s2); g.Load(lv);
    Surface wall=new Surface(W,H);
    for(int y=0;y<H;y++)for(int x=0;x<W;x++){ float u=y/(float)H; wall.P[y*W+x]=G.Mix(G.Hex(0x1f4a6a),G.Hex(0x4e3a76),u+0.12f*(float)Math.Sin(x*0.015));}
    for(int i=0;i<4;i++){ G.Rect(wall,10,12+i*36,24,20,G.Hex(0xe8c860)); G.Rect(wall,12,34+i*36,20,3,G.Hex(0xffffff)); }
    G.Rect(wall,300,40,250,150,G.Hex(0xf2f2f2)); G.Rect(wall,300,40,250,11,G.Hex(0x2a2a38)); G.Rect(wall,330,90,150,11,G.Hex(0xffffff)); G.RectO(wall,330,90,150,11,G.Hex(0x8a98aa));
    G.Rect(wall,0,H-16,W,16,G.Hex(0x1a1a22));
    List<DeskPlat> dp=new List<DeskPlat>();
    DeskPlat d=new DeskPlat(); d.X1=300;d.X2=550;d.Y=40;d.Id=1;d.Kind=0; dp.Add(d);
    d=new DeskPlat(); d.X1=330;d.X2=480;d.Y=90;d.Id=2;d.Kind=2; dp.Add(d);
    for(int i=0;i<4;i++){ d=new DeskPlat(); d.X1=10;d.X2=34;d.Y=12+i*36;d.Id=10+i;d.Kind=1; dp.Add(d);} g.SetDesk(dp);
    Input inp2=new Input(); Surface o=new Surface(W,H); int fr=0;
    using(var w=new BinaryWriter(File.Create("out/game.raw"))){
      Action shot=delegate(){ g.Render(inp2); if((fr++)%2==0){ for(int i=0;i<o.P.Length;i++) o.P[i]= g.Fb.P[i]==G.Key? wall.P[i]: g.Fb.P[i]; Raw(o,w);} };
      for(int k=0;k<27;k++){ inp2.Latch(); g.Tick(inp2); shot(); }
      foreach(byte ac in acts) for(int k=0;k<4;k++){ inp2.Down[(int)Key.Left]=Bot.ActDir(ac)<0; inp2.Down[(int)Key.Right]=Bot.ActDir(ac)>0; inp2.Down[(int)Key.Jump]=Bot.ActJump(ac); inp2.Down[(int)Key.Down]=Bot.ActDown(ac); inp2.Latch(); g.Tick(inp2); shot(); }
      for(int k=0;k<4;k++) inp2.Down[k]=false; inp2.Down[(int)Key.Jump]=false;
      for(int k=0;k<230;k++){ inp2.Latch(); g.Tick(inp2); shot(); }
    }
    Console.WriteLine(g.DebugState+" frames="+fr);
  }
}}
