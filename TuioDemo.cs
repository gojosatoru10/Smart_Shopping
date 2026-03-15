/*
	TUIO C# Demo - part of the reacTIVision project
	Copyright (c) 2005-2016 Martin Kaltenbrunner <martin@tuio.org>

	This program is free software; you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation; either version 2 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program; if not, write to the Free Software
	Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
*/

using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using TUIO;

public class TuioDemo : Form, TuioListener
{
    private TuioClient client;
    private Dictionary<long, TuioObject> objectList;
    private Dictionary<long, TuioCursor> cursorList;
    private Dictionary<long, TuioBlob> blobList;

    public static int width, height;
    private int window_width = 1920;
    private int window_height = 1080;
    private int window_left = 0;
    private int window_top = 0;
    private int screen_width = Screen.PrimaryScreen.Bounds.Width;
    private int screen_height = Screen.PrimaryScreen.Bounds.Height;

    private bool fullscreen;
    private bool verbose;
    public bool home = true, login = false, clothes = false, checkout = false, dark = false;

    public string themePath = @"Light";

    public DateTime themeSwitch = DateTime.MinValue;
    public DateTime pageSwitch = DateTime.MinValue;
    public int cooldownSeconds = 1;
    public int pageCooldown = 1;

    Font font = new Font("Arial", 10.0f);
    SolidBrush fntBrush = new SolidBrush(Color.White);
    SolidBrush bgrBrush = new SolidBrush(Color.FromArgb(0, 0, 64));
    SolidBrush curBrush = new SolidBrush(Color.FromArgb(192, 0, 192));
    SolidBrush objBrush = new SolidBrush(Color.FromArgb(64, 0, 0));
    SolidBrush blbBrush = new SolidBrush(Color.FromArgb(64, 64, 64));
    Pen curPen = new Pen(new SolidBrush(Color.Blue), 1);

    public TuioDemo(int port)
    {

        verbose = false;
        fullscreen = true;
        width = window_width;
        height = window_height;

        this.ClientSize = new System.Drawing.Size(width, height);
        this.Name = "TuioDemo";
        this.Text = "TuioDemo";

        this.Closing += new CancelEventHandler(Form_Closing);
        this.KeyDown += new KeyEventHandler(Form_KeyDown);

        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                        ControlStyles.UserPaint |
                        ControlStyles.DoubleBuffer, true);

        objectList = new Dictionary<long, TuioObject>(128);
        cursorList = new Dictionary<long, TuioCursor>(128);
        blobList = new Dictionary<long, TuioBlob>(128);

        client = new TuioClient(port);
        client.addTuioListener(this);

        client.connect();
    }

    private void Form_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {

        if (e.KeyData == Keys.F1)
        {
            if (fullscreen == false)
            {

                width = screen_width;
                height = screen_height;

                window_left = this.Left;
                window_top = this.Top;

                this.FormBorderStyle = FormBorderStyle.None;
                this.Left = 0;
                this.Top = 0;
                this.Width = screen_width;
                this.Height = screen_height;

                fullscreen = true;
            }
            else
            {

                width = window_width;
                height = window_height;

                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.Left = window_left;
                this.Top = window_top;
                this.Width = window_width;
                this.Height = window_height;

                fullscreen = false;
            }
        }
        else if (e.KeyData == Keys.Escape)
        {
            this.Close();

        }
        else if (e.KeyData == Keys.V)
        {
            verbose = !verbose;
        }

    }

    private void Form_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        client.removeTuioListener(this);

        client.disconnect();
        System.Environment.Exit(0);
    }

    public void addTuioObject(TuioObject o)
    {
        lock (objectList)
        {
            objectList.Add(o.SessionID, o);
        }
        if (verbose) Console.WriteLine("add obj " + o.SymbolID + " (" + o.SessionID + ") " + o.X + " " + o.Y + " " + o.Angle);
    }

    public void updateTuioObject(TuioObject o)
    {

        if (verbose) Console.WriteLine("set obj " + o.SymbolID + " " + o.SessionID + " " + o.X + " " + o.Y + " " + o.Angle + " " + o.MotionSpeed + " " + o.RotationSpeed + " " + o.MotionAccel + " " + o.RotationAccel);
    }

    public void removeTuioObject(TuioObject o)
    {
        lock (objectList)
        {
            objectList.Remove(o.SessionID);
        }
        if (verbose) Console.WriteLine("del obj " + o.SymbolID + " (" + o.SessionID + ")");
    }

    public void addTuioCursor(TuioCursor c)
    {
        lock (cursorList)
        {
            cursorList.Add(c.SessionID, c);
        }
        if (verbose) Console.WriteLine("add cur " + c.CursorID + " (" + c.SessionID + ") " + c.X + " " + c.Y);
    }

    public void updateTuioCursor(TuioCursor c)
    {
        if (verbose) Console.WriteLine("set cur " + c.CursorID + " (" + c.SessionID + ") " + c.X + " " + c.Y + " " + c.MotionSpeed + " " + c.MotionAccel);
    }

    public void removeTuioCursor(TuioCursor c)
    {
        lock (cursorList)
        {
            cursorList.Remove(c.SessionID);
        }
        if (verbose) Console.WriteLine("del cur " + c.CursorID + " (" + c.SessionID + ")");
    }

    public void addTuioBlob(TuioBlob b)
    {
        lock (blobList)
        {
            blobList.Add(b.SessionID, b);
        }
        if (verbose) Console.WriteLine("add blb " + b.BlobID + " (" + b.SessionID + ") " + b.X + " " + b.Y + " " + b.Angle + " " + b.Width + " " + b.Height + " " + b.Area);
    }

    public void updateTuioBlob(TuioBlob b)
    {

        if (verbose) Console.WriteLine("set blb " + b.BlobID + " (" + b.SessionID + ") " + b.X + " " + b.Y + " " + b.Angle + " " + b.Width + " " + b.Height + " " + b.Area + " " + b.MotionSpeed + " " + b.RotationSpeed + " " + b.MotionAccel + " " + b.RotationAccel);
    }

    public void removeTuioBlob(TuioBlob b)
    {
        lock (blobList)
        {
            blobList.Remove(b.SessionID);
        }
        if (verbose) Console.WriteLine("del blb " + b.BlobID + " (" + b.SessionID + ")");
    }

    public void refresh(TuioTime frameTime)
    {
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Getting the graphics object
        Graphics g = pevent.Graphics;
        g.FillRectangle(bgrBrush, new Rectangle(0, 0, width, height));



        void ResizeImage(ref Bitmap img)
        {
            try
            {
                img = new Bitmap(img, new Size(ClientSize.Width, ClientSize.Height));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error resizing image: " + ex.Message);
            }
        }

        /// Takes the current page from other functions and displays it
        void Display_Current_Page(Bitmap currentPage)
        {
            try
            {
                g.DrawImage(currentPage, new Rectangle(0, 0, currentPage.Width, currentPage.Height));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error displaying image: " + ex.Message);
            }
        }
        ///

        /// Draws The Home Screen
        void DrawHomeScreen()
        {
            Bitmap img = new Bitmap(themePath + @"\\home.png");
            ResizeImage(ref img);
            Display_Current_Page(img);
        }

        if (home == true)
        {
            DrawHomeScreen();
        }
        ///


        /// Draws The Login Screen
        void DrawLoginScreen()
        {
            Bitmap img = new Bitmap(themePath + @"\\Login.png");
            ResizeImage(ref img);
            Display_Current_Page(img);
        }
        if (login == true)
        {
            DrawLoginScreen();
        }
        ///

        /// Draws The Clothes Screen
        void DrawClothesScreen()
        {
            Bitmap img = new Bitmap(themePath + @"\\ClothesBlack.png");
            ResizeImage(ref img);
            Display_Current_Page(img);
        }
        if (clothes == true)
        {
            DrawClothesScreen();
        }
        ///


        /// Draws The Checkout Screen
        void DrawCheckoutScreen()
        {
            Bitmap img = new Bitmap(themePath + @"\\Checkout.png");
            ResizeImage(ref img);
            Display_Current_Page(img);
        }
        if (checkout == true)
        {
            DrawCheckoutScreen();
        }
        ///

        // draw the cursor path
        if (cursorList.Count > 0)
        {
            lock (cursorList)
            {
                foreach (TuioCursor tcur in cursorList.Values)
                {
                    List<TuioPoint> path = tcur.Path;
                    TuioPoint current_point = path[0];

                    for (int i = 0; i < path.Count; i++)
                    {
                        TuioPoint next_point = path[i];
                        g.DrawLine(curPen, current_point.getScreenX(width), current_point.getScreenY(height), next_point.getScreenX(width), next_point.getScreenY(height));
                        current_point = next_point;
                    }
                    g.FillEllipse(curBrush, current_point.getScreenX(width) - height / 100, current_point.getScreenY(height) - height / 100, height / 50, height / 50);
                    g.DrawString(tcur.CursorID + "", font, fntBrush, new PointF(tcur.getScreenX(width) - 10, tcur.getScreenY(height) - 10));
                }
            }
        }

        // draw the objects
        if (objectList.Count > 0)
        {
            lock (objectList)
            {
                foreach (TuioObject tobj in objectList.Values)
                {
                    int ox = tobj.getScreenX(width);
                    int oy = tobj.getScreenY(height);
                    int size = height / 10;

                    if (tobj.SymbolID == 0)
                    {
                        if ((DateTime.Now - themeSwitch).TotalSeconds > cooldownSeconds)
                        {
                            dark = !dark;
                            themeSwitch = DateTime.Now;

                            if (!dark)
                            {
                                themePath = @"Light";
                            }
                            else
                            {
                                themePath = @"Dark";
                            }
                        }
                    }

                    if (tobj.SymbolID == 2)
                    {
                        if ((DateTime.Now - pageSwitch).TotalSeconds > pageCooldown)
                        {
                            pageSwitch = DateTime.Now;
                            if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 90)
                            {
                                if (home == true)
                                {
                                    home = false;
                                    login = true;
                                }
                                else if (login == true)
                                {
                                    login = false;
                                    clothes = true;
                                }
                                else if (clothes == true)
                                {
                                    clothes = false;
                                    checkout = true;
                                }
                                else if (checkout == true)
                                {
                                    checkout = false;
                                    home = true;
                                }
                            }

                            else if (tobj.AngleDegrees > 270 && tobj.AngleDegrees < 340)
                            {
                                if (login == true)
                                {
                                    login = false;
                                    home = true;
                                }
                                else if (clothes == true)
                                {
                                    clothes = false;
                                    login = true;
                                }
                                else if (checkout == true)
                                {
                                    checkout = false;
                                    clothes = true;
                                }
                                else if (home == true)
                                {
                                    home = false;
                                    checkout = true;
                                }
                            }
                        }


                        g.TranslateTransform(ox, oy);
                        g.RotateTransform((float)(tobj.Angle / Math.PI * 180.0f));
                        g.TranslateTransform(-ox, -oy);

                        g.FillRectangle(objBrush, new Rectangle(ox - size / 2, oy - size / 2, size, size));

                        g.TranslateTransform(ox, oy);
                        g.RotateTransform(-1 * (float)(tobj.Angle / Math.PI * 180.0f));
                        g.TranslateTransform(-ox, -oy);

                        g.DrawString(tobj.AngleDegrees + "", font, fntBrush, new PointF(ox - 10, oy - 10));
                    }
                }
            }




            // draw the blobs
            if (blobList.Count > 0)
            {
                lock (blobList)
                {
                    foreach (TuioBlob tblb in blobList.Values)
                    {
                        int bx = tblb.getScreenX(width);
                        int by = tblb.getScreenY(height);
                        float bw = tblb.Width * width;
                        float bh = tblb.Height * height;

                        g.TranslateTransform(bx, by);
                        g.RotateTransform((float)(tblb.Angle / Math.PI * 180.0f));
                        g.TranslateTransform(-bx, -by);

                        g.FillEllipse(blbBrush, bx - bw / 2, by - bh / 2, bw, bh);

                        g.TranslateTransform(bx, by);
                        g.RotateTransform(-1 * (float)(tblb.Angle / Math.PI * 180.0f));
                        g.TranslateTransform(-bx, -by);

                        g.DrawString(tblb.BlobID + "", font, fntBrush, new PointF(bx, by));
                    }
                }
            }
        }
    }

    public static void Main(String[] argv)
    {
        int port = 0;
        switch (argv.Length)
        {
            case 1:
                port = int.Parse(argv[0], null);
                if (port == 0) goto default;
                break;
            case 0:
                port = 3333;
                break;
            default:
                Console.WriteLine("usage: mono TuioDemo [port]");
                System.Environment.Exit(0);
                break;
        }

        TuioDemo app = new TuioDemo(port);
        Application.Run(app);
    }
}
