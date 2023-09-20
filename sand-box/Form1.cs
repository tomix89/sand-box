using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace sand_box {
    public partial class Form1 : Form {

        const int CANVAS_SIZE_X = 320; 
        const int CANVAS_SIZE_Y = 180;
        const int zoomFactor = 3;

        Random rnd = new Random();

        [DebuggerDisplay("{ToString()}")]
        struct Particle {
            public Material material;
  
            public override string ToString() {
                return material.ToString();
            }
        }

        enum Material {
            EMPTY = 0,
            WATER,
            SAND,
            SAND_STEEP,
            SAND_LONG_SETTLE,
            STEEL
        }

        private enum ScrollSpeed {
            STOPPED = 0,
            FPS_1,
            FPS_2,
            FPS_5,
            FPS_10,
            FPS_20,
            FPS_50,
            FPS_100,
        }

        Particle[,] canvas = new Particle[CANVAS_SIZE_X, CANVAS_SIZE_Y];
        int[] linearDisplayBuff = new int[CANVAS_SIZE_X * CANVAS_SIZE_Y];


        public Form1() {
            this.DoubleBuffered = true;
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e) {
            cBSimSpeed.DataSource = Enum.GetNames(typeof(ScrollSpeed));
            cBParticleType.DataSource = Enum.GetNames(typeof(Material));
        }

        // https://stackoverflow.com/questions/11456440/how-to-resize-a-bitmap-image-in-c-sharp-without-blending-or-filtering
        private Bitmap ResizeBitmap(Bitmap sourceBMP, int width, int height) {
            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result)) {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half; // to have true 'NearestNeighbor' on the right boundaries
                g.DrawImage(sourceBMP, 0, 0, width, height);
            }
            return result;
        }

        // https://stackoverflow.com/a/24315437
        private static Bitmap ImageFromRawArray(int[] arr, Bitmap output, int width, int height) {
            var rect = new Rectangle(0, 0, width, height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, output.PixelFormat);
            var ptr = bmpData.Scan0;
            Marshal.Copy(arr, 0, ptr, arr.Length);
            output.UnlockBits(bmpData);
            return output;
        }

        private void Form1_Paint(object sender, PaintEventArgs e) {
            // convert the abstract canvas to linear buffer colors for bitmap conversion
            int cntr = 0;
            for (int y = 0; y < CANVAS_SIZE_Y; ++y) {
                for (int x = 0; x < CANVAS_SIZE_X; ++x) {

                    switch (canvas[x, y].material) {
                        case Material.EMPTY:
                            linearDisplayBuff[cntr++] = unchecked((int)0xFF000000); // ARGB
                            break;

                        case Material.STEEL:
                            linearDisplayBuff[cntr++] = unchecked((int)0xFF808080); // ARGB
                            break;

                        case Material.SAND:
                            linearDisplayBuff[cntr++] = unchecked((int)0xFFFFA81A); // ARGB
                            break;

                        case Material.SAND_STEEP:
                            linearDisplayBuff[cntr++] = unchecked((int)0xFFFF781A); // ARGB
                            break;

                        case Material.SAND_LONG_SETTLE:
                            linearDisplayBuff[cntr++] = unchecked((int)0xFFAF781A); // ARGB
                            break;

                        case Material.WATER:
                            linearDisplayBuff[cntr++] = unchecked((int)0xFF0080FF); // ARGB
                            break;
                    }
                }
            }

            Bitmap screen = new Bitmap(CANVAS_SIZE_X, CANVAS_SIZE_Y, PixelFormat.Format32bppArgb);
            ImageFromRawArray(linearDisplayBuff, screen, CANVAS_SIZE_X, CANVAS_SIZE_Y);
            Bitmap resized = ResizeBitmap(screen, CANVAS_SIZE_X * zoomFactor, CANVAS_SIZE_Y * zoomFactor);
            e.Graphics.DrawImage(resized, 10, 10);
            resized.Dispose();
            screen.Dispose();
        }

        private void cBScrollSpeed_SelectedIndexChanged(object sender, EventArgs e) {
            ScrollSpeed ss = (ScrollSpeed)cBSimSpeed.SelectedIndex;

            if (cBSimSpeed.SelectedIndex == 0) {
                timer1.Stop();
            } else {
                timer1.Interval = (int)Math.Round(2148.7190921026 * Math.Exp(-0.76487189348237 * cBSimSpeed.SelectedIndex));
                timer1.Start();
                Console.WriteLine(timer1.Interval);
            }
        }

        private bool isCoordOnCanvas(int x, int y) {
            if (x >= CANVAS_SIZE_X) return false;
            if (x < 0) return false;
            if (y >= CANVAS_SIZE_Y) return false;
            if (y < 0) return false;

            return true;
        }

        bool isEmpty(int x, int y, Material askingMAterial) {

            if (!isCoordOnCanvas(x ,y)) {
                return false;
            }
            
            switch (askingMAterial) {
                case Material.SAND:
                case Material.SAND_STEEP:
                case Material.SAND_LONG_SETTLE:
                    return canvas[x, y].material == Material.EMPTY || canvas[x, y].material == Material.WATER;
                case Material.WATER:
                    return canvas[x, y].material == Material.EMPTY;
            }

            return false;
        }




        void evaluatePixel(int x, int y) {



            int yOffset = 999;
            switch (canvas[x, y].material) {
                case Material.EMPTY:
                case Material.STEEL:
                    return;
                case Material.SAND:
                    yOffset = 1; // increasing this number makes the piles steeper
                    break;
                case Material.SAND_STEEP:
                    yOffset = 3; // increasing this number makes the piles steeper
                    break;
                case Material.SAND_LONG_SETTLE:
                    // making the offset randomized makes the pile to move (settle) even after no more sand is being poured in
                    yOffset = rnd.Next(2, 16);

                    // below one looks better but is lot more expensive to calc
                    // yOffset = (int)Math.Round(2.0 + Math.Sqrt(rnd.NextDouble()) * 2.0);
                    break;
                case Material.WATER:
                    yOffset = 0; // next to the particle
                    break;
            }

            // down has priority
            if (isEmpty(x, y + 1, canvas[x, y].material)) {
                // swap the two particles
                Particle ptcl = canvas[x, y];

                canvas[x, y] = canvas[x, y + 1];
                canvas[x, y + 1] = ptcl;
                return;
            }

            // else calculate the right and left
            bool canGoRight = true;
            bool canGoLeft = true;

            Material currentMaterial = canvas[x, y].material;
            int currOffset = 0;
            while (canGoRight && currOffset <= yOffset) {
                canGoRight &= isEmpty(x + 1, y + currOffset, currentMaterial);
                currOffset++;
            }

            currOffset = 0;
            while (canGoLeft && currOffset <= yOffset) {
                canGoLeft &= isEmpty(x - 1, y + currOffset, currentMaterial);
                currOffset++;
            }

            if (canGoRight && canGoLeft) {
                // to reduce the movement noise stop moving back and forth in empty space
                // on the other hand allow with low probability to overcome stacking (if sand comes through)
                if ((canvas[x, y].material == Material.WATER) && (rnd.NextDouble() > 0.01)) {
                    canGoRight = false;
                    canGoLeft = false;
                } else {
                    int dice = rnd.Next(2);
                    canGoRight = (dice == 0);
                    canGoLeft = (dice != 0);
                }
            }

            if (canGoRight) {
                // swap the two particles
                Particle ptcl = canvas[x, y];

                canvas[x, y] = canvas[x + 1, y];
                canvas[x + 1, y] = ptcl;
            }

            if (canGoLeft) {
                // swap the two particles
                Particle ptcl = canvas[x, y];

                canvas[x, y] = canvas[x - 1, y];
                canvas[x - 1, y] = ptcl;
            }

        }

        void doSimulate() {

            // true random
            for (int id = 0; id < CANVAS_SIZE_X * CANVAS_SIZE_Y; ++id) {
                Point pt = new Point(rnd.Next(0, CANVAS_SIZE_X), rnd.Next(0, CANVAS_SIZE_Y));
                evaluatePixel(pt.X, pt.Y);
            }


            /*
                // to eliminate the artefacts caused by update direction
                // we randomize the directions
                bool xFirst = rnd.Next(2) > 0;
                bool xDir = rnd.Next(2) > 0;
                bool yDir = rnd.Next(2) > 0;


                if (xFirst) {
                    for (int x = (xDir ? 0 : CANVAS_SIZE_X-1); x != (xDir ? CANVAS_SIZE_X : 0); x = (xDir ? (++x) : (--x))) {
                        for (int y = (yDir ? 0 : CANVAS_SIZE_Y-1); y != (yDir ? CANVAS_SIZE_Y : 0); y = (yDir ? (++y) : (--y))) {
                            evaluatePixel(x, y);
                        }
                    }
                } else {
                    for (int y = (yDir ? 0 : CANVAS_SIZE_Y-1); y != (yDir ? CANVAS_SIZE_Y : 0); y = (yDir ? (++y) : (--y))) {
                        for (int x = (xDir ? 0 : CANVAS_SIZE_X-1); x != (xDir ? CANVAS_SIZE_X : 0); x = (xDir ? (++x) : (--x))) {
                            evaluatePixel(x, y);
                        }
                    }
                }
                */


            /*   for (int x = 0; x < CANVAS_SIZE_X; ++x) {
                   for (int y = 0; y < CANVAS_SIZE_Y; ++y) {
                        evaluatePixel(x, y);
                   }
               }*/


            if (checkBox1.Checked) {

                if (rnd.NextDouble() > 0.00005) {
                    Particle particle = new Particle();
                    particle.material = Material.SAND;
                    canvas[rnd.Next((int)(CANVAS_SIZE_X * 0.47), (int)(CANVAS_SIZE_X * 0.53)), 0] = particle;
                }

                if (rnd.NextDouble() > 0.00005) {
                    Particle particle = new Particle();
                    particle.material = Material.SAND_STEEP;
                    canvas[rnd.Next((int)(CANVAS_SIZE_X * 0.73), (int)(CANVAS_SIZE_X * 0.79)), 0] = particle;
                }


                if (rnd.NextDouble() > 0.00005) {
                    Particle particle = new Particle();
                    particle.material = Material.WATER;
                     canvas[rnd.Next((int)(CANVAS_SIZE_X * 0.01), (int)(CANVAS_SIZE_X * 0.05)), 0] = particle;
                }

                if (rnd.NextDouble() > 0.00005) {
                    Particle particle = new Particle();
                    particle.material = Material.WATER;
                    canvas[rnd.Next((int)(CANVAS_SIZE_X * 0.01), (int)(CANVAS_SIZE_X * 0.05)), 0] = particle;
                }

                if (rnd.NextDouble() > 0.00005) {
                    Particle particle = new Particle();
                    particle.material = Material.WATER;
                    canvas[rnd.Next((int)(CANVAS_SIZE_X * 0.85), (int)(CANVAS_SIZE_X * 0.89)), 0] = particle;
                }

                if (rnd.NextDouble() > 0.00005) {
                    Particle particle = new Particle();
                    particle.material = Material.WATER;
                    canvas[rnd.Next((int)(CANVAS_SIZE_X * 0.85), (int)(CANVAS_SIZE_X * 0.89)), 0] = particle;
                }


            }
        }




        private Point getMouseScaledPos() {
            var relativePoint = this.PointToClient(Cursor.Position);
            label2.Text = relativePoint.ToString();
            // we have an offset of 10, 10

            relativePoint.X = (relativePoint.X - 10) / zoomFactor;
            relativePoint.Y = (relativePoint.Y - 10) / zoomFactor;

            label2.Text += '\n' + relativePoint.ToString();

            return relativePoint;
        }


        private void timer1_Tick(object sender, EventArgs e) {
            doSimulate();

            // the draw will be called
            this.Invalidate();
        }


        int lastMouseUpdate = Environment.TickCount;

        private void drawParticleAtMouse() {
            Point pt = getMouseScaledPos();
            if (isCoordOnCanvas(pt.X, pt.Y)) {
                Particle particle = new Particle();
                particle.material = (Material)cBParticleType.SelectedIndex;

                canvas[pt.X, pt.Y] = particle;

                // throttle the updates as it sows down the simulation
                if (Environment.TickCount - lastMouseUpdate > 20) {
                    lastMouseUpdate = Environment.TickCount;
                    // the draw will be called
                    this.Invalidate();
                }
            }
        }


        bool isMouseDown = false;
        //----------------------- mouse handling -----------------------
        private void Form1_MouseDown(object sender, MouseEventArgs e) {
            isMouseDown = true;
            drawParticleAtMouse();
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e) {
            isMouseDown = false;
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e) {
            getMouseScaledPos();

            if (isMouseDown) {
                drawParticleAtMouse();
            }
        }
    }
}
