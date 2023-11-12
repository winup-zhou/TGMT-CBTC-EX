using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Zbx1425.DXDynamicTexture;

namespace TGMTAts.OBCU {

    public static class TGMTPainter {

        public static GDIHelper hHMI, hTDT;
        public static void Initialize() {
            var imgDir = Config.ImageAssetPath;

            hHMI = new GDIHelper(1024, 1024);
            hTDT = new GDIHelper(256, 256);

            hmi = new Bitmap(Path.Combine(imgDir, "hmi.png"));
            ackcmd = new Bitmap(Path.Combine(imgDir, "ackcmd.png"));
            atoctrl = new Bitmap(Path.Combine(imgDir, "atoctrl.png"));
            dormode = new Bitmap(Path.Combine(imgDir, "dormode.png"));
            dorrel = new Bitmap(Path.Combine(imgDir, "dorrel.png"));
            drvmode = new Bitmap(Path.Combine(imgDir, "drvmode.png"));
            emergency = new Bitmap(Path.Combine(imgDir, "emergency.png"));
            fault = new Bitmap(Path.Combine(imgDir, "fault.png"));
            selmode = new Bitmap(Path.Combine(imgDir, "selmode.png"));
            sigmode = new Bitmap(Path.Combine(imgDir, "sigmode.png"));
            special = new Bitmap(Path.Combine(imgDir, "special.png"));
            stopsig = new Bitmap(Path.Combine(imgDir, "stopsig.png"));
            departure = new Bitmap(Path.Combine(imgDir, "departure.png"));
            menu = new Bitmap(Path.Combine(imgDir, "menu.png"));

            num0 = new Bitmap(Path.Combine(imgDir, "num0.png"));
            numn0 = new Bitmap(Path.Combine(imgDir, "num-0.png"));
            colon = new Bitmap(Path.Combine(imgDir, "colon.png"));

            
            tdtbackoff = new Bitmap(Path.Combine(imgDir, "tdt_back_off.png"));
            tdtbackred = new Bitmap(Path.Combine(imgDir, "tdt_back_red.png"));
            tdtbackgreen = new Bitmap(Path.Combine(imgDir, "tdt_back_green.png"));
            tdtdigitsred = Image.FromFile(Path.Combine(imgDir, "tdt_digits_red.png"));
            tdtdigitsgreen = Image.FromFile(Path.Combine(imgDir, "tdt_digits_green.png"));
        }

        public static void Dispose() {
            hHMI.Dispose();
            hTDT.Dispose();
        }
        
        public static GDIHelper PaintHMI(AtsEx.PluginHost.Native.VehicleState state) {
            hHMI.BeginGDI();
            hHMI.DrawImage(hmi, 0, 0);

            hHMI.DrawImage(menu, 681, 66, TGMTAts.panel_[23] * 64, 64);
            hHMI.DrawImage(drvmode, 589, 133, TGMTAts.panel_[24] * 64, 64);
            hHMI.DrawImage(sigmode, 686, 133, TGMTAts.panel_[25] * 64, 64);
            hHMI.DrawImage(stopsig, 686, 200, TGMTAts.panel_[26] * 64, 64);
            hHMI.DrawImage(dorrel, 589, 267, TGMTAts.panel_[27] * 64, 64);
            hHMI.DrawImage(dormode, 589, 337, TGMTAts.panel_[28] * 64, 64);
            hHMI.DrawImage(departure, 686, 267, TGMTAts.panel_[32] * 64, 64);
            hHMI.DrawImage(emergency, 686, 337, TGMTAts.panel_[29] * 64, 64);
            hHMI.DrawImage(fault, 589, 405, TGMTAts.panel_[30] * 64, 64);
            hHMI.DrawImage(special, 686, 405, TGMTAts.panel_[31] * 64, 64);
            hHMI.DrawImage(ackcmd, 490, 472, TGMTAts.panel_[35] * 100, 100);
            hHMI.DrawImage(atoctrl, 32, 405, TGMTAts.panel_[21] * 64, 64);
            hHMI.DrawImage(selmode, 150, 405, TGMTAts.panel_[22] * 64, 64);

            if (TGMTAts.panel_[18] == 0) {
                hHMI.DrawImage(num0, 64, 120, D(TGMTAts.panel_[17], 0) * 18, 18);
                hHMI.DrawImage(numn0, 50, 120, D(TGMTAts.panel_[17], 1) * 18, 18);
                hHMI.DrawImage(numn0, 36, 120, D(TGMTAts.panel_[17], 2) * 18, 18);
            }
            hHMI.DrawImage(num0, 289, 212, D((int)Math.Abs(Math.Ceiling(state.Speed)), 0) * 18, 18);
            hHMI.DrawImage(numn0, 275, 212, D((int)Math.Abs(Math.Ceiling(state.Speed)), 1) * 18, 18);

            hHMI.DrawImage(num0, 562, 31, D(TGMTAts.TrainNumber, 0) * 18, 18);
            hHMI.DrawImage(num0, 548, 31, D(TGMTAts.TrainNumber, 1) * 18, 18);
            hHMI.DrawImage(num0, 534, 31, D(TGMTAts.TrainNumber, 2) * 18, 18);
            hHMI.DrawImage(num0, 520, 31, D(TGMTAts.TrainNumber, 3) * 18, 18);
            hHMI.DrawImage(num0, 506, 31, D(TGMTAts.TrainNumber, 4) * 18, 18);

            hHMI.DrawImage(num0, 648, 31, D(TGMTAts.DestinationNumber, 0) * 18, 18);
            hHMI.DrawImage(num0, 634, 31, D(TGMTAts.DestinationNumber, 1) * 18, 18);
            hHMI.DrawImage(num0, 620, 31, D(TGMTAts.DestinationNumber, 2) * 18, 18);

            var sec = Convert.ToInt32(state.Time.TotalMilliseconds) / 1000 % 60;
            var min = Convert.ToInt32(state.Time.TotalMilliseconds) / 1000 / 60 % 60;
            var hrs = Convert.ToInt32(state.Time.TotalMilliseconds) / 1000 / 3600 % 60;
            hHMI.DrawImage(num0, 186, 552, D(hrs, 1) * 18, 18);
            hHMI.DrawImage(num0, 200, 552, D(hrs, 0) * 18, 18);
            hHMI.DrawImage(num0, 228, 552, D(min, 1) * 18, 18);
            hHMI.DrawImage(num0, 242, 552, D(min, 0) * 18, 18);
            hHMI.DrawImage(num0, 270, 552, D(sec, 1) * 18, 18);
            hHMI.DrawImage(num0, 284, 552, D(sec, 0) * 18, 18);
            if (sec % 2 == 0) {
                hHMI.DrawImage(colon, 214, 552);
                hHMI.DrawImage(colon, 256, 552);
            }
            hHMI.EndGDI();

            hHMI.Graphics.FillRectangle(overspeed[TGMTAts.panel_[10]], new Rectangle(20, 18, 80, 78));
            hHMI.Graphics.FillRectangle(targetColor[TGMTAts.panel_[13] * 1 + TGMTAts.panel_[14] * 2], new Rectangle(68, 354 - TGMTAts.panel_[11], 10, TGMTAts.panel_[11]));
            if (TGMTAts.panel_[36] != 0 && TGMTAts.time % 500 < 250) {
                hHMI.Graphics.DrawRectangle(ackPen, new Rectangle(488, 470, 280, 100));
            }

            var tSpeed = ((double)TGMTAts.panel_[1] / 400 * 288 - 144) / 180 * Math.PI;
            hHMI.Graphics.DrawEllipse(circlePen, new Rectangle(255, 188, 66, 66));
            hHMI.Graphics.DrawLine(needlePen, Poc(288, 221, 33, 0, tSpeed), Poc(288, 221, 125, 0, tSpeed));
            hHMI.Graphics.FillPolygon(Brushes.White, new Point[] {
                Poc(288, 221, 163, 0, tSpeed), Poc(288, 221, 123, -5, tSpeed), Poc(288, 221, 123, 5, tSpeed)
            });
            if (TGMTAts.panel_[15] >= 0) {
                var tRecommend = ((double)TGMTAts.panel_[15] / 400 * 288 - 144) / 180 * Math.PI;
                hHMI.Graphics.FillPolygon(Brushes.Yellow, new Point[] {
                    Poc(288, 221, 165, 0, tRecommend), Poc(288, 221, 185, -11, tRecommend), Poc(288, 221, 185, 11, tRecommend)
                });
            }
            if (TGMTAts.panel_[16] >= 0) {
                var tLimit = ((double)TGMTAts.panel_[16] / 400 * 288 - 144) / 180 * Math.PI;
                hHMI.Graphics.FillPolygon(Brushes.Red, new Point[] {
                    Poc(288, 221, 165, 0, tLimit), Poc(288, 221, 185, -11, tLimit), Poc(288, 221, 185, 11, tLimit)
                });
            }
            return hHMI;
        }

        static int last101 = 0, last102 = 0;
        public static GDIHelper PaintTDT(AtsEx.PluginHost.Native.VehicleState state) {
            if (TGMTAts.panel_[101] == last101 && TGMTAts.panel_[102] == last102) return null;
            hTDT.BeginGDI();
            Image digitImage;
            if (TGMTAts.panel_[102] == -1) {
                hTDT.DrawImage(tdtbackred, 0, 0);
                digitImage = tdtdigitsred;
            } else if (TGMTAts.panel_[102] == 1) {
                hTDT.DrawImage(tdtbackgreen, 0, 0);
                digitImage = tdtdigitsgreen;
            } else {
                hTDT.DrawImage(tdtbackoff, 0, 0);
                digitImage = null;
            }
            hTDT.EndGDI();
            if (digitImage != null) {
                for (int i = 0; i <= 2; i++) {
                    var xpos = 152 - 55 * i;
                    hTDT.Graphics.SetClip(new Rectangle(xpos, 67, 60, 120));
                    var di = D(TGMTAts.panel_[101], i);
                    if (di == 10) di = 0;
                    hTDT.Graphics.DrawImageUnscaled(digitImage, xpos, 67 - 120 * di);
                }
            }

            last101 = TGMTAts.panel_[101]; last102 = TGMTAts.panel_[102];
            return hTDT;
        }

        static int[] pow10 = new int[] { 1, 10, 100, 1000, 10000, 100000 };

        static int D(int src, int digit) {
            if (pow10[digit] > src) {
                return 10;
            } else if (digit == 0 && src == 0) {
                return 0;
            } else {
                return src / pow10[digit] % 10;
            }
        }

        static Point Poc(int cx, int cy, int dr, int dt, double theta) {
            return new Point(
                (int)(cx + dr * Math.Sin(theta) + dt * Math.Cos(theta)),
                (int)(cy - dr * Math.Cos(theta) + dt * Math.Sin(theta))
            );
        }

        static Pen needlePen = new Pen(Color.White, 10);
        static Pen circlePen = new Pen(Color.White, 5);
        static Pen ackPen = new Pen(Color.Yellow, 4);
        static Brush[] targetColor = new Brush[] { new SolidBrush(Color.Red), new SolidBrush(Color.Orange), new SolidBrush(Color.Green) };
        static Brush[] overspeed = new Brush[] { new SolidBrush(Color.Empty), new SolidBrush(Color.Orange), new SolidBrush(Color.Red) };
        static Bitmap hmi, ackcmd, atoctrl, dormode, dorrel, drvmode, emergency, fault, departure, menu,
            selmode, sigmode, special, stopsig, num0, numn0, colon;
        static Bitmap tdtbackoff, tdtbackred, tdtbackgreen;
        static Image tdtdigitsred, tdtdigitsgreen;
    }
}
