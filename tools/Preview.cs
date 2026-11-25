using System; using System.IO;
namespace Deskosaur {
public static class Img {
  public static void Save(Surface s, string path, int scale) {
    using (var f = new FileStream(path, FileMode.Create)) {
      var hdr = System.Text.Encoding.ASCII.GetBytes("P6\n" + (s.W*scale) + " " + (s.H*scale) + "\n255\n");
      f.Write(hdr,0,hdr.Length);
      byte[] row = new byte[s.W*scale*3];
      for (int y=0;y<s.H;y++){ for(int x=0;x<s.W;x++){ int c=s.P[y*s.W+x]; for(int k=0;k<scale;k++){int o=(x*scale+k)*3; row[o]=(byte)(c>>16); row[o+1]=(byte)(c>>8); row[o+2]=(byte)c;} }
        for(int k=0;k<scale;k++) f.Write(row,0,row.Length); }
    }
  }
}
public static class PreviewMain {
  [STAThread]
  public static void Main(string[] a) {
    Surface s = new Surface(420, 300); G.Clear(s, G.Hex(0x3a3550));
    Pose[] poses = { Pose.Idle0, Pose.Idle1, Pose.Blink, Pose.Run0, Pose.Run1, Pose.Run2, Pose.Run3, Pose.Jump, Pose.Fall, Pose.Skid, Pose.Happy };
    for (int c=0;c<4;c++) {
      Outfit o = new Outfit(); o.Char=c;
      for (int i=0;i<poses.Length;i++){ int ax,ay; Sprite sp=Art.Compose(o,poses[i],0,out ax,out ay); G.Blit(s,sp,4+i*36+18-ax, 30+c*36-ay,false); }
      Outfit o2 = new Outfit(); o2.Char=c; o2.Hat=1+c*2; o2.Glasses=1+c; o2.Shirt=1+c*2; o2.Shoes=1+c;
      for (int i=0;i<4;i++){ int ax,ay; Outfit oo=o2.Clone(); oo.Hat=(1+c*2+i)%8; oo.Glasses=(c+i*2)%8; oo.Shirt=(c*2+i)%9; oo.Shoes=(c+i)%7; Sprite sp=Art.Compose(oo,Pose.Idle0,0,out ax,out ay); G.Blit(s,sp,4+i*36+18-ax, 180+c*30-ay+20,false);}
    }
    Img.Save(s, a[0], 3);
  }
}}
