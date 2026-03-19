
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
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

    /// Represents the root file system path for assets.
    private readonly string assetRootPath;
    public string themePath;

    // Tracks the cumulative totals for each hoodie color
    public Dictionary<string, int> cart = new Dictionary<string, int>() {
    { "Black", 0 }, { "Grey", 0 }, { "Burgundy", 0 }, { "Pink", 0 }
};
    DateTime lastSelectionTime = DateTime.Now;

    /// <summary>
    /// Using the Time of the last switch and a cooldown to prevent multiple switches from one rotation, as the TUIO objects can update very quickly and we only want one switch per rotation.
    /// </summary>
    public DateTime themeSwitch = DateTime.MinValue;
    public DateTime pageSwitch = DateTime.MinValue;
    public DateTime hoodieSwitch = DateTime.MinValue;
    public int cooldownSeconds = 1;
    public int pageCooldown = 1;
    public int hoodieCooldown = 1;

    /// Hoodie color state variable to keep track of the current color and switch between them when the corresponding object is rotated.
    private string hoodieColor = "Black";

    /// checkout hoodie color state variable to keep track of the current color for the checkout page, allowing it to reflect the selected hoodie color from the clothes page.
    private string checkoutHodieColor = ""; 



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
        this.Text = "Smart Shopping";

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

        /// Resolve the asset root path and set the initial theme path to the Light theme. This allows for flexibility in where the assets are stored, making it easier to run the application in different environments without needing to change the code.
        assetRootPath = ResolveAssetRootPath();
        themePath = Path.Combine(assetRootPath, "Light");

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





        /// Resizes the give image to fit the screen.
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
        ///



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
            try
            {
                // 1. Draw Background
                Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png"));
                ResizeImage(ref bg);
                g.DrawImage(bg, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;

                // Draw Logo
                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                    {
                        g.DrawImage(logo, -100, 0, 400, 200);
                    }
                }
                catch { }

                // 2. Card Layout Calculations
                float cardWidth = cw * 0.22f;
                float cardHeight = ch * 0.35f;
                float spacing = cw * 0.06f;
                float startX = (cw - (3 * cardWidth + 2 * spacing)) / 2;
                float baseY = ch * 0.35f; 

                // 3. Color Logic
                Color cardColor;
                Color shadowColor;

                if (dark)
                {
                    cardColor = Color.FromArgb(130, 130, 130);
                    shadowColor = Color.FromArgb(70, 70, 70);
                }
                else
                {
                    cardColor = Color.FromArgb(222, 200, 150);
                    shadowColor = Color.FromArgb(180, 160, 110);
                }

                Brush cardBrush = new SolidBrush(cardColor);
                Brush shadowBrush = new SolidBrush(shadowColor);
                Font textFont = new Font("Vladimir Script", 36f, FontStyle.Regular);

                Brush textBrush;
                if (dark) 
                { 
                    textBrush = Brushes.White; 
                }
                else 
                { 
                    textBrush = Brushes.Black; 
                }

                string[] titles = { "Bestsellers", "Deals", "Outfit Builder" };
                string[] images = { "Bestsellers.png", "Deals.png", "OutfitBuilder.png" };

                // 4. Cursor Detection
                PointF cursorPoint = new PointF(-1000, -1000);
                if (cursorList.Count > 0)
                {
                    foreach (TuioCursor c in cursorList.Values)
                    {
                        cursorPoint = new PointF(c.getScreenX(width), c.getScreenY(height));
                        break;
                    }
                }

                // 5. Draw the Cards
                for (int i = 0; i < 3; i++)
                {
                    float x = startX + i * (cardWidth + spacing);
                    float y = baseY;

                    RectangleF cardRect = new RectangleF(x, y, cardWidth, cardHeight);

                    float shadowOffset = 10;

                    RectangleF liftedRect = new RectangleF(x, y, cardWidth, cardHeight);

                    // Shadow
                    RectangleF shadowRect = new RectangleF(x + shadowOffset, y + shadowOffset, cardWidth, cardHeight);
                    using (GraphicsPath shadowPath = RoundedRect(shadowRect, 30))
                    {
                        g.FillPath(shadowBrush, shadowPath);
                    }

                    // Main Card
                    using (GraphicsPath path = RoundedRect(liftedRect, 30))
                    {
                        g.FillPath(cardBrush, path);
                        using (Pen highlightPen = new Pen(Color.White, 3))
                        {
                            g.DrawPath(highlightPen, path);
                        }
                    }

                    // Image inside card
                    using (Bitmap img = new Bitmap(Path.Combine(themePath, images[i])))
                    {
                        float imgSize = cardWidth * 0.5f;
                        float imgX = liftedRect.X + (cardWidth - imgSize) / 2;
                        float imgY = liftedRect.Y + cardHeight * 0.15f;

                        using (GraphicsPath circle = new GraphicsPath())
                        {
                            circle.AddEllipse(imgX, imgY, imgSize, imgSize);
                            g.SetClip(circle);
                            g.DrawImage(img, new RectangleF(imgX, imgY, imgSize, imgSize));
                            g.ResetClip();
                        }

                        using (Pen imageBorderPen = new Pen(Color.White, 2))
                        {
                            // Smooth out the edges of the circle
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.DrawEllipse(imageBorderPen, imgX, imgY, imgSize, imgSize);
                        }
                    }

                    // Text inside card
                    SizeF textSize = g.MeasureString(titles[i], textFont);
                    float textX = liftedRect.X + (cardWidth - textSize.Width) / 2;
                    float textY = liftedRect.Y + cardHeight * 0.75f;
                    g.DrawString(titles[i], textFont, textBrush, textX, textY);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error drawing Home Screen: " + ex.Message);
            }
        }

        if (home == true)
        {
            DrawHomeScreen();
        }
        ///


        /// Draws The Login Screen
        void DrawLoginScreen()
        {
            try
            {
                // 1. Draw Background
                Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png"));
                ResizeImage(ref bg);
                g.DrawImage(bg, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;

                // Draw Logo
                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                    {
                        g.DrawImage(logo, -100, 0, 400, 200);
                    }
                }
                catch { }

                // 3. Setup Colors (Matching your Login.jpg screenshot)
                Color circleColor;
                Color shadowColor;
                if (dark)
                {
                    circleColor = Color.FromArgb(130, 130, 130); // Using dark grey
                    shadowColor = Color.FromArgb(70, 70, 70);
                }
                else
                {
                    circleColor = Color.FromArgb(235, 215, 160); // Using creamy color 
                    shadowColor = Color.FromArgb(190, 170, 120);
                }

                Brush circleBrush = new SolidBrush(circleColor);
                Brush shadowBrush = new SolidBrush(shadowColor);
                Font loginFont = new Font("Vladimir Script", 66f, FontStyle.Regular);
                Brush textBrush = dark ? Brushes.White : Brushes.Black;

                // 4. Position Circles
                string[] labels = { "Login", "Signup" };
                float circleSize = cw * 0.25f; // Diameter of the circles
                float spacing = cw * 0.15f;    // Space between them
                float totalWidth = (2 * circleSize) + spacing;
                float startX = (cw - totalWidth) / 2;
                float centerY = (ch - circleSize) / 2;

                // 5. Drawing Loop
                for (int i = 0; i < 2; i++)
                {
                    float x = startX + i * (circleSize + spacing);
                    float y = centerY;

                    RectangleF circleRect = new RectangleF(x, y, circleSize, circleSize);

                    // Shadow Offset
                    float offset = 12f;
                    g.FillEllipse(shadowBrush, x + offset, y + offset, circleSize, circleSize);

                    // Main Circle
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(circleBrush, circleRect);

                    // White Border
                    using (Pen borderPen = new Pen(Color.White, 3))
                    {
                        g.DrawEllipse(borderPen, circleRect);
                    }

                    // Draw Text
                    SizeF textSize = g.MeasureString(labels[i], loginFont);
                    float tx = x + (circleSize - textSize.Width) / 2;
                    float ty = y + (circleSize - textSize.Height) / 2;
                    g.DrawString(labels[i], loginFont, textBrush, tx, ty);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error drawing Login Screen: " + ex.Message);
            }
        }
        if (login == true)
        {
            DrawLoginScreen();
        }
        ///


        /// Draws The Clothes Screen
        void DrawClothesScreen()
        {
<<<<<<< Updated upstream
            Bitmap img = new Bitmap(Path.Combine(themePath, $"Select{hoodieColor}.png"));
            ResizeImage(ref img);
            Display_Current_Page(img);
=======
            try
            {
                // 1. Draw Background
                Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png"));
                ResizeImage(ref bg);
                g.DrawImage(bg, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;

                // Draw Logo
                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                    {
                        g.DrawImage(logo, -100, 0, 400, 200);
                    }
                }
                catch { }

                // 3. Layout Setup
                string[] hoodieNames = { "Black", "Grey", "Burgundy", "Pink" };
                string[] hoodieFiles = { "BlackHoodie.png", "GreyHoodie.png", "BurgundyHoodie.png", "PinkHoodie.png" };
                int[] counts = { cthoodieBlack, cthoodieGrey, cthoodieBurgundy, cthoodiePink };

                float cardWidth = cw * 0.20f;
                float cardHeight = ch * 0.35f;
                float spacing = cw * 0.03f;
                float startX = (cw - (4 * cardWidth + 3 * spacing)) / 2;
                float baseY = ch * 0.30f;

                // 4. Color Logic
                Color cardColor, shadowColor, barColor, btnBaseColor, cartBtnColor, cartShadowColor;
                Brush textBrush, symbolBrush;
                Pen btnOutline;

                // Colors for each theme
                if (dark)
                {
                    cardColor = Color.FromArgb(130, 130, 130);
                    shadowColor = Color.FromArgb(70, 70, 70);
                    barColor = Color.FromArgb(55, 55, 55);
                    btnBaseColor = Color.FromArgb(70, 90, 120);
                    cartBtnColor = Color.FromArgb(70, 90, 120);
                    cartShadowColor = Color.FromArgb(70, 70, 70);
                    textBrush = Brushes.White;
                    symbolBrush = Brushes.Black;
                    btnOutline = new Pen(Color.Black, 2);
                }
                else
                {
                    cardColor = Color.FromArgb(235, 215, 160);
                    shadowColor = Color.FromArgb(180, 160, 110);
                    barColor = Color.FromArgb(220, 200, 150);
                    btnBaseColor = Color.FromArgb(255, 190, 100);
                    cartBtnColor = Color.FromArgb(255, 190, 100);
                    cartShadowColor = Color.FromArgb(180, 160, 110);
                    textBrush = Brushes.Black;
                    symbolBrush = Brushes.Black;
                    btnOutline = new Pen(Color.Black, 2);
                }

                g.SmoothingMode = SmoothingMode.AntiAlias;

                for (int i = 0; i < 4; i++)
                {
                    float x = startX + i * (cardWidth + spacing);
                    float y = baseY - 5f;

                    // A. Draw shadow
                    float shadowOffset = 10f;
                    RectangleF shadowRect = new RectangleF(x + shadowOffset, y + shadowOffset, cardWidth, cardHeight);
                    using (GraphicsPath shadowPath = RoundedRect(shadowRect, 25))
                    using (Brush sBrush = new SolidBrush(shadowColor))
                    {
                        g.FillPath(sBrush, shadowPath);
                    }

                    // B. Draw main card
                    RectangleF rect = new RectangleF(x, y, cardWidth, cardHeight);
                    using (GraphicsPath path = RoundedRect(rect, 25))
                    using (Brush bBrush = new SolidBrush(cardColor))
                    {
                        g.FillPath(bBrush, path);

                        if (hoodieColor == hoodieNames[i])
                        {
                            Color penColor;

                            if (dark)
                            {
                                penColor = Color.White;
                            }
                            else
                            {
                                penColor = Color.Black;
                            }

                            using (Pen selectPen = new Pen(penColor, 5))
                            {
                                g.DrawPath(selectPen, path);
                            }
                        }
                    }

                    // C. Draw hoodie image
                    try
                    {
                        using (Bitmap img = new Bitmap(Path.Combine(themePath, hoodieFiles[i])))
                        {
                            g.DrawImage(img, x, y - 10, cardWidth, cardHeight + 20);
                        }
                    }
                    catch { }

                    // D. Draw controls bar and +/- buttons
                    float controlY = ch * 0.70f;
                    float btnSize = 55f;
                    RectangleF barRect = new RectangleF(x, controlY, cardWidth, btnSize);
                    using (GraphicsPath barPath = RoundedRect(barRect, 30))
                    using (Brush barBrush = new SolidBrush(barColor)) g.FillPath(barBrush, barPath);

                    // Plus/Minus Buttons
                    bool plus = (hoodieColor == hoodieNames[i] && (DateTime.Now - hoodieCount).TotalMilliseconds < 800 && objectList.ContainsKey(3));
                    bool minus = (hoodieColor == hoodieNames[i] && (DateTime.Now - hoodieCount).TotalMilliseconds < 800 && objectList.ContainsKey(4));

                    // Draw Plus
                    RectangleF pRect = new RectangleF(x, controlY, btnSize, btnSize);

                    Color fillColor;

                    if (plus)
                    {
                        fillColor = Color.Gold;
                    }
                    else
                    {
                        fillColor = btnBaseColor;
                    }

                    using (Brush b = new SolidBrush(fillColor))
                    {
                        g.FillEllipse(b, pRect);
                        g.DrawEllipse(btnOutline, pRect);
                        g.DrawString("+", new Font("Arial", 22, FontStyle.Bold), symbolBrush, x + 14, controlY + 11);
                    }

                    // Draw Minus
                    float mX = x + cardWidth - btnSize;
                    RectangleF mRect = new RectangleF(mX, controlY, btnSize, btnSize);

                    if (minus)
                    {
                        fillColor = Color.Gold;
                    }
                    else
                    {
                        fillColor = btnBaseColor;
                    }

                    using (Brush b = new SolidBrush(fillColor))
                    {
                        g.FillEllipse(b, mRect);
                        g.DrawEllipse(btnOutline, mRect);
                        g.DrawString("-", new Font("Arial", 22, FontStyle.Bold), symbolBrush, mX + 17, controlY + 8);
                    }

                    // Draw Count Text
                    using (Font f = new Font("Arial", 28, FontStyle.Bold))
                    {
                        string s = counts[i].ToString();
                        float tx = x + (cardWidth - g.MeasureString(s, f).Width) / 2;
                        g.DrawString(s, f, textBrush, tx, controlY + 4);
                    }

                    // E. Draw "ADD TO CART" Button
                    float cartY = controlY + btnSize + 15f;
                    float cartH = 48f;
                    RectangleF cartRect = new RectangleF(x, cartY, cardWidth, cartH);

                    // Button Shadow
                    RectangleF cartShad = new RectangleF(x, cartY + 4, cardWidth, cartH);
                    using (GraphicsPath cSPath = RoundedRect(cartShad, 15))
                    using (Brush sBrush = new SolidBrush(cartShadowColor)) g.FillPath(sBrush, cSPath);

                    // Button Top
                    using (GraphicsPath cPath = RoundedRect(cartRect, 15))
                    using (Brush cBrush = new SolidBrush(cartBtnColor))
                    {
                        g.FillPath(cBrush, cPath);
                        Color penColor;

                        if (dark)
                        {
                            penColor = Color.White;
                        }
                        else
                        {
                            penColor = Color.Black;
                        }

                        using (Pen pen = new Pen(penColor, 1))
                        {
                            g.DrawPath(pen, cPath);
                        }

                        string txt = "ADD TO CART";
                        using (Font f = new Font("Segoe UI", 12, FontStyle.Bold))
                        {
                            SizeF sz = g.MeasureString(txt, f);
                            if (dark)
                            {
                                g.DrawString(txt, f, Brushes.White, x + (cardWidth - sz.Width) / 2, cartY + (cartH - sz.Height) / 2);
                            }
                            else
                            {
                                g.DrawString(txt, f, Brushes.Black, x + (cardWidth - sz.Width) / 2, cartY + (cartH - sz.Height) / 2);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Draw Error: " + ex.Message); }
>>>>>>> Stashed changes
        }
        if (clothes == true)
        {
            DrawClothesScreen();
        }
        ///


        /// Draws The Checkout Screen
        void DrawCheckoutScreen()
        {
<<<<<<< Updated upstream
            Bitmap img = new Bitmap(Path.Combine(themePath, $"Checkout{checkoutHodieColor}.png"));
            ResizeImage(ref img);
            Display_Current_Page(img);
=======
            try
            {
                // 1. Background and Logo
                using (Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png")))
                {
                    Bitmap tempBg = bg;
                    ResizeImage(ref tempBg);
                    g.DrawImage(tempBg, 0, 0);
                }
                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                        g.DrawImage(logo, -100, 0, 400, 200);
                }
                catch { }

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;
                int totalItems = cart.Sum(x => x.Value);

                float cardW = cw * 0.55f;
                float cardH = ch * 0.75f;
                float cardX = (cw - cardW) / 2;
                float cardY = (ch - cardH) / 2 + 40;

                // Theme Colors
                Color cardCol;
                Color shadowCol;

                if (dark)
                {
                    cardCol = Color.FromArgb(130, 130, 130);
                    shadowCol = Color.FromArgb(70, 70, 70);
                }
                else
                {
                    cardCol = Color.FromArgb(235, 215, 160);
                    shadowCol = Color.FromArgb(180, 160, 110);
                }

                // Checkout Button Colors
                Color btnCol;
                Color btnTextCol;

                if (dark)
                {
                    btnCol = Color.FromArgb(255, 190, 100);
                    btnTextCol = Color.Black;
                }
                else
                {
                    btnCol = Color.FromArgb(210, 180, 140);
                    btnTextCol = Color.FromArgb(50, 30, 0);
                }

                Brush textBrush;
                Color penColor;

                if (dark)
                {
                    textBrush = Brushes.White;
                    penColor = Color.White;
                }
                else
                {
                    textBrush = Brushes.Black;
                    penColor = Color.SaddleBrown;
                }

                Pen selectionPen = new Pen(penColor, 3);
                selectionPen.DashStyle = DashStyle.Solid;

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw Card
                g.FillPath(new SolidBrush(shadowCol), RoundedRect(new RectangleF(cardX + 10, cardY + 10, cardW, cardH), 30));
                g.FillPath(new SolidBrush(cardCol), RoundedRect(new RectangleF(cardX, cardY, cardW, cardH), 30));

                using (Font titleFont = new Font("Segoe UI Semibold", 24))
                using (Font itemFont = new Font("Segoe UI", 13, FontStyle.Bold))
                using (Font symbolFont = new Font("Arial", 18, FontStyle.Bold))
                {
                    if (totalItems == 0)
                    {
                        // Default checkout screen when cart is empty
                        g.DrawString("Order Summary", titleFont, textBrush, cardX + 50, cardY + 40);
                        string emptyMsg = "Your cart is empty";
                        using (Font emptyFont = new Font("Vladimir Script", 40, FontStyle.Bold))
                        {
                            SizeF sz = g.MeasureString(emptyMsg, emptyFont);
                            g.DrawString(emptyMsg, emptyFont, textBrush, cardX + (cardW - sz.Width) / 2, cardY + 150);
                        }
                    }
                    else
                    {
                        // Cart isn't empty
                        g.DrawString("Order Summary", titleFont, textBrush, cardX + 50, cardY + 30);

                        float itemY = cardY + 90;
                        double subtotal = 0;
                        string[] keys = { "Black", "Grey", "Burgundy", "Pink" };
                        string[] files = { "BlackHoodie.png", "GreyHoodie.png", "BurgundyHoodie.png", "PinkHoodie.png" };

                        for (int i = 0; i < keys.Length; i++)
                        {
                            int qty = cart[keys[i]];
                            if (qty > 0)
                            {
                                // Selection border
                                if (hoodieColor == keys[i])
                                {
                                    RectangleF selRect = new RectangleF(cardX + 30, itemY - 10, cardW - 60, 70);
                                    g.DrawPath(selectionPen, RoundedRect(selRect, 15));
                                }

                                try
                                {
                                    using (Bitmap hImg = new Bitmap(Path.Combine(themePath, files[i])))
                                        g.DrawImage(hImg, cardX + 45, itemY - 5, 60, 60);
                                }
                                catch { }

                                g.DrawString($"{keys[i]} Hoodie", itemFont, textBrush, cardX + 115, itemY + 15);

                                // Quantity Controls
                                float recX = cardX + 330;
                                RectangleF pillRect = new RectangleF(recX, itemY + 5, 110, 40);
                                using (Brush b = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                                    g.FillPath(b, RoundedRect(pillRect, 20));

                                g.DrawString("-", symbolFont, textBrush, recX + 12, itemY + 10);
                                g.DrawString(qty.ToString(), itemFont, textBrush, recX + 45, itemY + 14);
                                g.DrawString("+", symbolFont, textBrush, recX + 82, itemY + 10);

                                float price = qty * 50.00f;
                                subtotal += price;
                                string pStr = $"{price:F2}$";
                                g.DrawString(pStr, itemFont, textBrush, cardX + cardW - 60 - g.MeasureString(pStr, itemFont).Width, itemY + 15);

                                itemY += 75;
                            }
                        }

                        // Calculation Summary
                        itemY = cardY + cardH - 220;
                        double service = subtotal * 0.12;
                        double tax = subtotal * 0.14;
                        double totalVal = subtotal + service + tax;

                        string[,] summary = { { "Subtotal", $"{subtotal:F2}$" }, { "Service (12%)", $"{service:F2}$" }, { "TAX (14%)", $"{tax:F2}$" } };
                        for (int i = 0; i < 3; i++)
                        {
                            g.DrawString(summary[i, 0], itemFont, textBrush, cardX + 50, itemY + (i * 28));
                            g.DrawString(summary[i, 1], itemFont, textBrush, cardX + cardW - 60 - g.MeasureString(summary[i, 1], itemFont).Width, itemY + (i * 28));
                        }

                        // Total Section
                        float btnY = cardY + cardH - 95;
                        g.DrawLine(new Pen(textBrush, 2), cardX + 50, btnY - 15, cardX + cardW - 50, btnY - 15);

                        g.DrawString("Total Payment", itemFont, textBrush, cardX + 50, btnY + 20);
                        string totalStr = $"{totalVal:F2}$";
                        g.DrawString(totalStr, titleFont, textBrush, cardX + 180, btnY + 10);

                        // Checkout button
                        Color currentBtnCol;

                        if (dark)
                        {
                            currentBtnCol = Color.FromArgb(70, 90, 120);
                        }
                        else
                        {
                            currentBtnCol = Color.FromArgb(255, 190, 100);
                        }

                        float btnW = 180;
                        float btnH = 65;
                        float btnX = cardX + cardW - btnW - 40;
                        btnY = cardY + cardH - 95;

                        using (Brush b = new SolidBrush(currentBtnCol))
                        {
                            g.FillPath(b, RoundedRect(new RectangleF(btnX, btnY, btnW, btnH), 18));
                        }

                        // Draw the button outline
                        g.DrawPath(new Pen(Color.Black, 2), RoundedRect(new RectangleF(btnX, btnY, btnW, btnH), 18));

                        // Center the text inside the button
                        Color textColor;

                        if (dark)
                        {
                            textColor = Color.White;
                        }
                        else
                        {
                            textColor = Color.Black;
                        }

                        using (Brush bTxt = new SolidBrush(textColor))
                        {
                            string txt = "CHECKOUT";
                            SizeF s = g.MeasureString(txt, itemFont);
                            g.DrawString(txt, itemFont, bTxt,
                                btnX + (btnW - s.Width) / 2,
                                btnY + (btnH - s.Height) / 2);
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
>>>>>>> Stashed changes
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
                /// Define the order of hoodie colors for switching
                string[] hoodieOrder = { "Black", "Grey", "Burgundy", "Pink" };


                foreach (TuioObject tobj in objectList.Values)
                {
                    int ox = tobj.getScreenX(width);
                    int oy = tobj.getScreenY(height);
                    int size = height / 10;

                    /// Handle Theme Switching
                    if (tobj.SymbolID == 0)
                    {
                        if ((DateTime.Now - themeSwitch).TotalSeconds > cooldownSeconds)
                        {
                            dark = !dark;
                            themeSwitch = DateTime.Now;

                            if (!dark)
                            {
                                themePath = Path.Combine(assetRootPath, "Light"); ;
                            }
                            else
                            {
                                themePath = Path.Combine(assetRootPath, "Dark");
                            }
                        }
                    }
                    ///

                    /// Handle Hoodie Color Switching
                    if (tobj.SymbolID == 2)
                    {
                        if ((DateTime.Now - hoodieSwitch).TotalSeconds > hoodieCooldown)
                        {
                            hoodieSwitch = DateTime.Now;

                            int currentIndex = Array.IndexOf(hoodieOrder, hoodieColor);
                            if (currentIndex < 0) currentIndex = 0;

                            if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 90)
                            {
                                currentIndex = (currentIndex + 1) % hoodieOrder.Length;
                            }
                            else if (tobj.AngleDegrees > 270 && tobj.AngleDegrees < 340)
                            {
                                currentIndex = (currentIndex - 1 + hoodieOrder.Length) % hoodieOrder.Length;
                            }

                            hoodieColor = hoodieOrder[currentIndex];
                        }
                    }
<<<<<<< Updated upstream
                    ///
=======

                    // Handles the logic behind adjusting the hoodie quantity in the cart
                    if ((tobj.SymbolID == 3 || tobj.SymbolID == 4) && checkout)
                    {
                        if ((DateTime.Now - hoodieCount).TotalMilliseconds > 500)
                        {
                            // Only adjust if the hoodie is actually in the cart or being selected
                            int change;

                            if (tobj.SymbolID == 3)
                            {
                                change = 1;
                            }
                            else
                            {
                                change = -1;
                            }
                            cart[hoodieColor] = Math.Max(0, cart[hoodieColor] + change);
                            hoodieCount = DateTime.Now;
                        }
                    }

                    /// Handles the logic for adding and removing hoodies from the cart based on the rotation of the object with SymbolID 4, allowing users to adjust the quantity of the selected hoodie color before proceeding to checkout.
                    if (tobj.SymbolID == 3 && clothes)
                    {
                        if ((DateTime.Now - hoodieCount).TotalSeconds > hoodieCooldown)
                        {
                            hoodieCount = DateTime.Now;
                            if (hoodieColor == "Black")
                            {
                                cthoodieBlack++;
                              
                            }

                            if (hoodieColor == "Pink")
                            {
                                cthoodiePink++;
                              
                            }

                            if (hoodieColor == "Burgundy")
                            {
                                cthoodieBurgundy++;
                                this.Text = "" + cthoodieBurgundy;
                            }
                            if (hoodieColor == "Grey")
                            {
                                cthoodieGrey++;
                                
                            }
                        }
                    }

                    /// Handles Hoodie Count Decreasing, ensuring it doesn't go below 0 and only updates once per rotation using a cooldown.
                    if (tobj.SymbolID == 4 && clothes)
                    {
                        if ((DateTime.Now - hoodieCount).TotalSeconds > hoodieCooldown)
                        {
                            hoodieCount = DateTime.Now;
                            if (hoodieColor == "Black")
                            {
                                if (cthoodieBlack > 0)
                                {
                                    cthoodieBlack--;
                                }

                              
                            }

                            if (hoodieColor == "Pink")
                            {
                                if (cthoodiePink > 0)
                                {

                                    cthoodiePink--;
                                }
                              
                            }

                            if (hoodieColor == "Burgundy")
                            {
                                if (cthoodieBurgundy > 0)
                                {

                                    cthoodieBurgundy--;
                                }
                               
                            }
                            if (hoodieColor == "Grey")
                            {
                                if (cthoodieGrey > 0)
                                {

                                    cthoodieGrey--;
                                }
                            }
                        }
                    }

                    /// Handles "ADD TO CART" button
                    if (tobj.SymbolID == 5 && clothes)
                    {
                        // Use a cooldown to prevent rapid-fire adding while the object is placed
                        if ((DateTime.Now - hoodieSwitch).TotalMilliseconds > 1500)
                        {
                            int amountToAdd = 0;

                            // 1. Identify how many were selected in the current UI
                            if (hoodieColor == "Black") 
                            {
                                amountToAdd = cthoodieBlack; 
                                cthoodieBlack = 0; 
                            }
                            else if (hoodieColor == "Grey") 
                            { 
                                amountToAdd = cthoodieGrey; 
                                cthoodieGrey = 0; 
                            }
                            else if (hoodieColor == "Burgundy") 
                            { 
                                amountToAdd = cthoodieBurgundy; 
                                cthoodieBurgundy = 0; 
                            }
                            else if (hoodieColor == "Pink") 
                            { 
                                amountToAdd = cthoodiePink; 
                                cthoodiePink = 0; 
                            }

                            // 2. Add to the cumulative cart (Summing them up)
                            if (amountToAdd > 0)
                            {
                                cart[hoodieColor] += amountToAdd;
                                Console.WriteLine($"Added {amountToAdd} {hoodieColor} hoodies. Total in cart: {cart[hoodieColor]}");

                                // Set the timestamp for cooldown
                                hoodieSwitch = DateTime.Now;
                            }
                        }
                    }

                    /// Handles the logic for proceeding to checkout when the object with SymbolID 5 is rotated, ensuring that the user has selected a hoodie color and quantity before allowing them to move to the checkout page.
                    if (tobj.SymbolID == 6 && clothes)
                    {
                        if (hoodieColor == "Black")
                        {
                            clothes = false;
                            checkout = true;
                        }

                        if (hoodieColor == "Pink")
                        {
                            clothes = false;
                            checkout = true;
                        }

                        if (hoodieColor == "Burgundy")
                        {
                            clothes = false;
                            checkout = true;
                        }
                        if (hoodieColor == "Grey")
                        {
                            clothes = false;
                            checkout = true;
                        }
                    }

                    /// Handles the logic for confirming the purchase and displaying the thank you screen when the object with SymbolID 6 is rotated on the checkout page, allowing users to complete their transaction and receive confirmation of their order.
                    if (tobj.SymbolID == 6&&checkout)
                    {

                        checkout = false;
                        clothes = false;
                        home = false;
                        login = false;
                        thankyou = true;

                    }
>>>>>>> Stashed changes

                    ///Handle Page Switching
                    if (tobj.SymbolID == 1)
                    {
                        if ((DateTime.Now - pageSwitch).TotalSeconds > pageCooldown)
                        {
                            pageSwitch = DateTime.Now;
                            /// The page switching logic is based on the current page and the direction of rotation. If the object is rotated clockwise (between 20 and 90 degrees), it moves to the next page in the sequence. If it is rotated counterclockwise (between 270 and 340 degrees), it moves to the previous page. The sequence of pages is Home -> Login -> Clothes -> Checkout -> Home.
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
                            ///
                            
                            /// The counterclockwise rotation logic is the reverse of the clockwise logic, allowing users to navigate back through the pages in the opposite direction.
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
                            ///
                        }
                    }
                    ///

                    /// Only draw the objects if they are the ones we are using for page switching or hoodie color switching
                    /// 

                    // Handles the logic for switching between hoodie colors using the object with SymbolID 2, allowing users to cycle through available hoodie options in their cart by rotating the object while on the checkout page.
                    if (tobj.SymbolID == 2 && checkout)
                    {
                        if ((DateTime.Now - lastSelectionTime).TotalMilliseconds > 500)
                        {
                            // 1. Manually build a list of keys that have a quantity > 0
                            List<string> itemsInCart = new List<string>();

                            foreach (KeyValuePair<string, int> entry in cart)
                            {
                                if (entry.Value > 0)
                                {
                                    itemsInCart.Add(entry.Key);
                                }
                            }

                            // 2. Perform the rotation if the cart isn't empty
                            if (itemsInCart.Count > 0)
                            {
                                int currentIndex = -1;

                                // Find the index of the current hoodieColor manually
                                for (int i = 0; i < itemsInCart.Count; i++)
                                {
                                    if (itemsInCart[i] == hoodieColor)
                                    {
                                        currentIndex = i;
                                        break;
                                    }
                                }

                                // Calculate next index and update hoodieColor
                                int nextIndex = (currentIndex + 1) % itemsInCart.Count;
                                hoodieColor = itemsInCart[nextIndex];
                            }

                            lastSelectionTime = DateTime.Now;
                        }
                    }

                    if (tobj.SymbolID == 1 || tobj.SymbolID == 2)
                    {
                        g.TranslateTransform(ox, oy);
                        g.RotateTransform((float)(tobj.Angle / Math.PI * 180.0f));
                        g.TranslateTransform(-ox, -oy);

                        g.FillRectangle(objBrush, new Rectangle(ox - size / 2, oy - size / 2, size, size));

                        g.TranslateTransform(ox, oy);
                        g.RotateTransform(-1 * (float)(tobj.Angle / Math.PI * 180.0f));
                        g.TranslateTransform(-ox, -oy);

                        g.DrawString(tobj.AngleDegrees + "", font, fntBrush, new PointF(ox - 10, oy - 10));
                    }
                    ///
                }
                this.Invalidate();
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

    // Draws rouded rectangles, used for buttons and cards in the UI
    private GraphicsPath RoundedRect(RectangleF bounds, int radius)
    {
        float diameter = radius * 2;
        SizeF size = new SizeF(diameter, diameter);
        RectangleF arc = new RectangleF(bounds.Location, size);
        GraphicsPath path = new GraphicsPath();

        // top left
        path.AddArc(arc, 180, 90);

        // top right
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);

        // bottom right
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // bottom left
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Resolves the root path to the assets directory based on the application's base directory.
    /// </summary>
    /// <returns>The full path to the assets directory if found; otherwise, the application's base directory.</returns>
    private static string ResolveAssetRootPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string projectAssets = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "Assests"));

        if (Directory.Exists(projectAssets))
        {
            return projectAssets;
        }

        string localAssets = Path.Combine(baseDir, "Assests");
        if (Directory.Exists(localAssets))
        {
            return localAssets;
        }

        return baseDir;
    }
    ///

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

