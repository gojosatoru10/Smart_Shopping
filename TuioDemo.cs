
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
    public bool home = true, login = false, clothes = false, checkout = false, dark = false, thankyou = false, bestsellers = false, deals = false, outfitbuilder = false;
    int ctBlackJacket = 0, ctDenimJacket = 0, ctDenimPants = 0, ctNavyShirt = 0, ctBlackPants = 0, ctBurgundyShirt = 0, ctBlackHoodie = 0, ctPinkHoodie = 0;

    /// Represents the root file system path for assets.
    private readonly string assetRootPath;
    public string themePath;

    // Expanded Dictionary for all items
    // Ensure these strings match bestNames exactly!
    Dictionary<string, int> cart = new Dictionary<string, int> {
    { "Navy Shirt", 0 },
    { "Black Hoodie", 0 },
    { "Denim Pants", 0 },
    { "Black Jacket", 0 },
    { "Pink Hoodie", 0 },
    { "Burgundy Shirt", 0 },
    { "Black Pants", 0 },
    { "Denim Jacket", 0 },
    { "Black", 0 }, { "Grey", 0 }, { "Burgundy", 0 }, { "Pink", 0 }
};
    string[] bestNames = { "Navy Shirt", "Black Hoodie", "Denim Pants", "Black Jacket", "Pink Hoodie", "Burgundy Shirt", "Black Pants", "Denim Jacket" };
    string[] dealNames = { "Navy Shirt", "Black Hoodie", "Denim Pants", "Black Jacket", "Pink Hoodie", "Burgundy Shirt", "Black Pants", "Denim Jacket" };

    // Selection and Scroll tracking
    int scrollIndex = 0; // Current starting card
    DateTime lastScrollTime = DateTime.Now;
    DateTime lastSelectionTime = DateTime.Now;

    // --- Outfit Builder Globals ---
    string currentTop;    // Default starting top
    string currentBottom; // Default starting bottom
    int cartScrollIndex = 0; // For scrolling through cart items in outfit builder

    // --- Scroll Tracking for Outfit Builder ---
    // Index 0: Shirts, 1: Hoodies, 2: Jackets, 3: Pants, 4: Shorts
    int[] scrollIndices = new int[5] { 0, 0, 0, 0, 0 };

    // --- Item Arrays for Outfit Builder ---
    string[][] items = {
    new string[] { "BlackShirt", "BrownShirt", "BurgundyShirt", "DarkBlueShirt", "NavyShirt", "PinkShirt", "PrintedShirt", "WhiteShirt" },
    new string[] { "BlackHoodie", "BurgundyHoodie", "DarkGreyHoodie", "GreenHoodie", "GreyHoodie", "LightBlueHoodie", "OffWhiteHoodie", "PinkHoodie" },
    new string[] { "BlackJacket", "BrownFurJacket", "BrownJacket", "DenimFurJacket", "DenimJacket", "FadedJacket", "GreyBuffyJacket", "GreyJacket" },
    new string[] { "BlackCargoPants", "BlueDenimPants", "BlackPants", "DenimCargoPants", "DenimPants", "FadedDenimPants", "GreyCargoPants", "NavyDenimPants" },
    new string[] { "BlackShorts", "BlueShorts", "GreyDenimShorts", "GreyShorts", "LightDenimShorts", "NavyDenimShorts", "WhiteShorts" }
};

    /// <summary>
    /// Using the Time of the last switch and a cooldown to prevent multiple switches from one rotation, as the TUIO objects can update very quickly and we only want one switch per rotation.
    /// </summary>
    public DateTime themeSwitch = DateTime.MinValue;
    public DateTime pageSwitch = DateTime.MinValue;
    public DateTime hoodieSwitch = DateTime.MinValue;
    public int cooldownSeconds = 1;
    public int pageCooldown = 1;
    public int hoodieCooldown = 1;

    int selectedHomeCard = 0; // 0: Bestsellers, 1: Deals, 2: Outfit Builder
    DateTime homeSwitchTime = DateTime.Now;

    /// Hoodie color state variable to keep track of the current color and switch between them when the corresponding object is rotated.
    private string hoodieColor = "Black";

    /// checkout hoodie color state variable to keep track of the current color for the checkout page, allowing it to reflect the selected hoodie color from the clothes page.
    private string checkoutHodieColor = "";

    private int cthoodieBlack = 0;
    private int cthoodieGrey = 0;
    private int cthoodieBurgundy = 0;
    private int cthoodiePink = 0;

    private DateTime hoodieCount = DateTime.MinValue;
    DateTime lastOutfitSelectTime = DateTime.MinValue;

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
                using (Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png")))
                {
                    Bitmap tempBg = bg;
                    ResizeImage(ref tempBg);
                    g.DrawImage(tempBg, 0, 0);
                }

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;

                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                        g.DrawImage(logo, -100, 0, 400, 200);
                }
                catch { }

                // 2. Card Layout
                float cardWidth = cw * 0.22f;
                float cardHeight = ch * 0.35f;
                float spacing = cw * 0.06f;
                float startX = (cw - (3 * cardWidth + 2 * spacing)) / 2;
                float baseY = ch * 0.35f;

                // 3. Colors
                Color cardColor = dark ? Color.FromArgb(130, 130, 130) : Color.FromArgb(222, 200, 150);
                Color shadowColor = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(180, 160, 110);
                Color selectionColor = dark ? Color.White : Color.FromArgb(100, 70, 40); // Selection border color

                Brush textBrush = dark ? Brushes.White : Brushes.Black;
                Font textFont = new Font("Vladimir Script", 36f, FontStyle.Regular);

                string[] titles = { "Bestsellers", "Deals", "Outfit Builder" };
                string[] images = { "Bestsellers.png", "Deals.png", "OutfitBuilder.png" };

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 4. Draw the Cards
                for (int i = 0; i < 3; i++)
                {
                    bool isSelected = (selectedHomeCard == i);

                    float x = startX + i * (cardWidth + spacing);
                    float y = isSelected ? baseY - 15 : baseY; // Lift selected card slightly
                    float shadowOffset = isSelected ? 15 : 10;

                    RectangleF cardRect = new RectangleF(x, y, cardWidth, cardHeight);
                    RectangleF shadowRect = new RectangleF(x + shadowOffset, y + shadowOffset, cardWidth, cardHeight);

                    // Draw Shadow
                    using (GraphicsPath shadowPath = RoundedRect(shadowRect, 30))
                        g.FillPath(new SolidBrush(shadowColor), shadowPath);

                    // Draw Selection Border (The "Animation" look)
                    if (isSelected)
                    {
                        using (GraphicsPath borderPath = RoundedRect(new RectangleF(x - 5, y - 5, cardWidth + 10, cardHeight + 10), 35))
                        using (Pen selPen = new Pen(selectionColor, 4))
                        {
                            g.DrawPath(selPen, borderPath);
                        }
                    }

                    // Draw Main Card
                    using (GraphicsPath path = RoundedRect(cardRect, 30))
                    {
                        g.FillPath(new SolidBrush(cardColor), path);
                        using (Pen highlightPen = new Pen(isSelected ? selectionColor : Color.White, isSelected ? 3 : 1))
                            g.DrawPath(highlightPen, path);
                    }

                    // Draw Image inside circle
                    try
                    {
                        using (Bitmap img = new Bitmap(Path.Combine(themePath, images[i])))
                        {
                            float imgSize = cardWidth * 0.55f;
                            float imgX = cardRect.X + (cardWidth - imgSize) / 2;
                            float imgY = cardRect.Y + cardHeight * 0.15f;

                            using (GraphicsPath circle = new GraphicsPath())
                            {
                                circle.AddEllipse(imgX, imgY, imgSize, imgSize);
                                g.SetClip(circle);
                                g.DrawImage(img, new RectangleF(imgX, imgY, imgSize, imgSize));
                                g.ResetClip();
                            }
                            using (Pen imgPen = new Pen(Color.White, 2))
                                g.DrawEllipse(imgPen, imgX, imgY, imgSize, imgSize);
                        }
                    }
                    catch { }

                    // Draw Text
                    SizeF textSize = g.MeasureString(titles[i], textFont);
                    g.DrawString(titles[i], textFont, textBrush, cardRect.X + (cardWidth - textSize.Width) / 2, cardRect.Y + cardHeight * 0.75f);
                }
            }
            catch (Exception ex) { Console.WriteLine("Error: " + ex.Message); }
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
        //void DrawClothesScreen()
        //{
        //    try
        //    {
        //        // 1. Draw Background
        //        using (Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png")))
        //        {
        //            Bitmap tempBg = bg;
        //            ResizeImage(ref tempBg);
        //            g.DrawImage(tempBg, 0, 0);
        //        }

        //        float cw = ClientSize.Width;
        //        float ch = ClientSize.Height;

        //        // Draw Logo
        //        try
        //        {
        //            using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
        //                g.DrawImage(logo, -100, 0, 400, 200);
        //        }
        //        catch { }

        //        // 2. Layout Setup
        //        string[] hoodieNames = { "Black", "Grey", "Burgundy", "Pink" };
        //        string[] hoodieFiles = { "BlackHoodie.png", "GreyHoodie.png", "BurgundyHoodie.png", "PinkHoodie.png" };
        //        int[] counts = { cthoodieBlack, cthoodieGrey, cthoodieBurgundy, cthoodiePink };

        //        float cardWidth = cw * 0.20f;
        //        float cardHeight = ch * 0.35f;
        //        float spacing = cw * 0.03f;
        //        float startX = (cw - (4 * cardWidth + 3 * spacing)) / 2;
        //        float baseY = ch * 0.30f;

        //        // 3. Theme Colors
        //        Color cardColor = dark ? Color.FromArgb(130, 130, 130) : Color.FromArgb(235, 215, 160);
        //        Color shadowColor = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(180, 160, 110);
        //        Color barColor = dark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(220, 200, 150);
        //        Color btnBaseColor = dark ? Color.FromArgb(70, 90, 120) : Color.FromArgb(255, 190, 100);
        //        Color selectionColor = dark ? Color.White : Color.FromArgb(100, 70, 40); // Same as Home Screen

        //        Brush textBrush = dark ? Brushes.White : Brushes.Black;
        //        Pen btnOutline = new Pen(Color.Black, 2);

        //        g.SmoothingMode = SmoothingMode.AntiAlias;

        //        for (int i = 0; i < 4; i++)
        //        {
        //            bool isSelected = (hoodieColor == hoodieNames[i]);

        //            float x = startX + i * (cardWidth + spacing);
        //            // ANIMATION: Lift the card if selected
        //            float y = isSelected ? baseY - 15 : baseY;
        //            float shadowOffset = isSelected ? 15 : 10;

        //            RectangleF rect = new RectangleF(x, y, cardWidth, cardHeight);
        //            RectangleF shadowRect = new RectangleF(x + shadowOffset, y + shadowOffset, cardWidth, cardHeight);

        //            // A. Draw Shadow
        //            using (GraphicsPath shadowPath = RoundedRect(shadowRect, 25))
        //                g.FillPath(new SolidBrush(shadowColor), shadowPath);

        //            // B. Draw Secondary Selection Border (Same as Home Screen)
        //            if (isSelected)
        //            {
        //                using (GraphicsPath borderPath = RoundedRect(new RectangleF(x - 5, y - 5, cardWidth + 10, cardHeight + 10), 30))
        //                using (Pen selPen = new Pen(selectionColor, 4))
        //                {
        //                    g.DrawPath(selPen, borderPath);
        //                }
        //            }

        //            // C. Draw Main Card
        //            using (GraphicsPath path = RoundedRect(rect, 25))
        //            {
        //                g.FillPath(new SolidBrush(cardColor), path);
        //                using (Pen highlightPen = new Pen(isSelected ? selectionColor : Color.White, isSelected ? 3 : 1))
        //                    g.DrawPath(highlightPen, path);
        //            }

        //            // D. Draw Hoodie Image
        //            try
        //            {
        //                using (Bitmap imgg = new Bitmap(Path.Combine(themePath, hoodieFiles[i])))
        //                    g.DrawImage(imgg, x, y - 10, cardWidth, cardHeight + 20);
        //            }
        //            catch { }

        //            // E. Controls Bar (Fixed position relative to card)
        //            float controlY = ch * 0.70f;
        //            float btnSize = 55f;
        //            RectangleF barRect = new RectangleF(x, controlY, cardWidth, btnSize);
        //            using (GraphicsPath barPath = RoundedRect(barRect, 30))
        //                g.FillPath(new SolidBrush(barColor), barPath);

        //            // Plus/Minus Logic
        //            bool plusPressed = (isSelected && (DateTime.Now - hoodieCount).TotalMilliseconds < 800 && objectList.ContainsKey(3));
        //            bool minusPressed = (isSelected && (DateTime.Now - hoodieCount).TotalMilliseconds < 800 && objectList.ContainsKey(4));

        //            // Plus Button
        //            RectangleF pRect = new RectangleF(x, controlY, btnSize, btnSize);
        //            using (Brush b = new SolidBrush(plusPressed ? Color.Gold : btnBaseColor))
        //            {
        //                g.FillEllipse(b, pRect);
        //                g.DrawEllipse(btnOutline, pRect);
        //                g.DrawString("+", new Font("Arial", 22, FontStyle.Bold), Brushes.Black, x + 14, controlY + 11);
        //            }

        //            // Minus Button
        //            float mX = x + cardWidth - btnSize;
        //            RectangleF mRect = new RectangleF(mX, controlY, btnSize, btnSize);
        //            using (Brush b = new SolidBrush(minusPressed ? Color.Gold : btnBaseColor))
        //            {
        //                g.FillEllipse(b, mRect);
        //                g.DrawEllipse(btnOutline, mRect);
        //                g.DrawString("-", new Font("Arial", 22, FontStyle.Bold), Brushes.Black, mX + 17, controlY + 8);
        //            }

        //            // Quantity Count
        //            using (Font f = new Font("Arial", 28, FontStyle.Bold))
        //            {
        //                string s = counts[i].ToString();
        //                float tx = x + (cardWidth - g.MeasureString(s, f).Width) / 2;
        //                g.DrawString(s, f, textBrush, tx, controlY + 4);
        //            }

        //            // F. ADD TO CART Button
        //            float cartY = controlY + btnSize + 15f;
        //            float cartH = 48f;
        //            RectangleF cartRect = new RectangleF(x, cartY, cardWidth, cartH);

        //            using (Brush sBrush = new SolidBrush(shadowColor))
        //                g.FillPath(sBrush, RoundedRect(new RectangleF(x, cartY + 4, cardWidth, cartH), 15));

        //            using (Brush cBrush = new SolidBrush(btnBaseColor))
        //            {
        //                g.FillPath(cBrush, RoundedRect(cartRect, 15));
        //                using (Pen pen = new Pen(dark ? Color.White : Color.Black, 1))
        //                    g.DrawPath(pen, RoundedRect(cartRect, 15));

        //                string txt = "ADD TO CART";
        //                using (Font f = new Font("Segoe UI", 12, FontStyle.Bold))
        //                {
        //                    SizeF sz = g.MeasureString(txt, f);
        //                    g.DrawString(txt, f, textBrush, x + (cardWidth - sz.Width) / 2, cartY + (cartH - sz.Height) / 2);
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex) { Console.WriteLine("Draw Error: " + ex.Message); }
        //}
        //if (clothes == true)
        //{
        //    //DrawClothesScreen();
        //}

        // Draws The Bestsellers Screen
        void DrawBestsellersScreen()
        {
            try
            {
                // 1. Draw Background
                using (Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png")))
                {
                    Bitmap tempBg = bg;
                    ResizeImage(ref tempBg);
                    g.DrawImage(tempBg, 0, 0);
                }

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;

                // Draw Logo
                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                        g.DrawImage(logo, -100, 0, 400, 200);
                }
                catch { }

                // 2. Layout Setup
                string[] bestNames = { "Navy Shirt", "Black Hoodie", "Denim Pants", "Black Jacket", "Pink Hoodie", "Burgundy Shirt", "Black Pants", "Denim Jacket" };
                string[] bestFiles = { "NavyShirt.png", "BlackHoodie.png", "DenimPants.png", "BlackJacket.png", "PinkHoodie.png", "BurgundyShirt.png", "BlackPants.png", "DenimJacket.png" };

                // Map the hard-coded variables to an array so we can access them by index
                // Make sure these variables (ctNavyShirt, etc.) are declared at the top of your script
                int[] counts = { ctNavyShirt, cthoodieBlack, ctDenimPants, ctBlackJacket, cthoodiePink, cthoodieBurgundy, ctBlackPants, ctDenimJacket };

                float cardWidth = cw * 0.20f;
                float cardHeight = ch * 0.35f;
                float spacing = cw * 0.03f;
                float startX = (cw - (4 * cardWidth + 3 * spacing)) / 2;
                float baseY = ch * 0.30f;

                // 3. Theme Colors
                Color cardColor = dark ? Color.FromArgb(130, 130, 130) : Color.FromArgb(235, 215, 160);
                Color shadowColor = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(180, 160, 110);
                Color barColor = dark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(220, 200, 150);
                Color btnBaseColor = dark ? Color.FromArgb(70, 90, 120) : Color.FromArgb(255, 190, 100);
                Color selectionColor = dark ? Color.White : Color.FromArgb(100, 70, 40);

                Brush textBrush = dark ? Brushes.White : Brushes.Black;
                Pen btnOutline = new Pen(Color.Black, 2);

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 4. Draw 4 visible cards starting from scrollIndex
                for (int i = 0; i < 4; i++)
                {
                    int itemIdx = (scrollIndex + i) % bestNames.Length;
                    string currentItemName = bestNames[itemIdx];
                    string currentItemFile = bestFiles[itemIdx];

                    // Selection Logic: The white border stays on the first card (scrollIndex)
                    bool isSelected = (hoodieColor == currentItemName);

                    float x = startX + i * (cardWidth + spacing);
                    float y = isSelected ? baseY - 15 : baseY;
                    float shadowOffset = isSelected ? 15 : 10;

                    RectangleF rect = new RectangleF(x, y, cardWidth, cardHeight);
                    RectangleF shadowRect = new RectangleF(x + shadowOffset, y + shadowOffset, cardWidth, cardHeight);

                    // A. Draw Shadow
                    using (GraphicsPath shadowPath = RoundedRect(shadowRect, 25))
                        g.FillPath(new SolidBrush(shadowColor), shadowPath);

                    // B. Draw Selection Border
                    if (isSelected)
                    {
                        using (GraphicsPath borderPath = RoundedRect(new RectangleF(x - 5, y - 5, cardWidth + 10, cardHeight + 10), 30))
                        using (Pen selPen = new Pen(selectionColor, 4))
                            g.DrawPath(selPen, borderPath);
                    }

                    // C. Draw Main Card
                    using (GraphicsPath path = RoundedRect(rect, 25))
                    {
                        g.FillPath(new SolidBrush(cardColor), path);
                        using (Pen highlightPen = new Pen(isSelected ? selectionColor : Color.White, isSelected ? 3 : 1))
                            g.DrawPath(highlightPen, path);
                    }

                    // D. Draw Product Image
                    try
                    {
                        using (Bitmap imgg = new Bitmap(Path.Combine(themePath, currentItemFile)))
                            g.DrawImage(imgg, x, y - 10, cardWidth, cardHeight + 20);
                    }
                    catch { }

                    // E. Controls Bar (+ / -)
                    float controlY = ch * 0.70f;
                    float btnSize = 55f;
                    RectangleF barRect = new RectangleF(x, controlY, cardWidth, btnSize);
                    using (GraphicsPath barPath = RoundedRect(barRect, 30))
                        g.FillPath(new SolidBrush(barColor), barPath);

                    bool plusPressed = (isSelected && (DateTime.Now - hoodieCount).TotalMilliseconds < 800 && objectList.ContainsKey(3));
                    bool minusPressed = (isSelected && (DateTime.Now - hoodieCount).TotalMilliseconds < 800 && objectList.ContainsKey(4));

                    // Plus Button
                    RectangleF pRect = new RectangleF(x, controlY, btnSize, btnSize);
                    using (Brush b = new SolidBrush(plusPressed ? Color.Gold : btnBaseColor))
                    {
                        g.FillEllipse(b, pRect);
                        g.DrawEllipse(btnOutline, pRect);
                        g.DrawString("+", new Font("Arial", 22, FontStyle.Bold), Brushes.Black, x + 14, controlY + 11);
                    }

                    // Minus Button
                    float mX = x + cardWidth - btnSize;
                    RectangleF mRect = new RectangleF(mX, controlY, btnSize, btnSize);
                    using (Brush b = new SolidBrush(minusPressed ? Color.Gold : btnBaseColor))
                    {
                        g.FillEllipse(b, mRect);
                        g.DrawEllipse(btnOutline, mRect);
                        g.DrawString("-", new Font("Arial", 22, FontStyle.Bold), Brushes.Black, mX + 17, controlY + 8);
                    }

                    // Quantity Count (Pulls from our mapped array)
                    using (Font f = new Font("Arial", 28, FontStyle.Bold))
                    {
                        string s = counts[itemIdx].ToString();
                        float tx = x + (cardWidth - g.MeasureString(s, f).Width) / 2;
                        g.DrawString(s, f, textBrush, tx, controlY + 4);
                    }

                    // F. ADD TO CART Button
                    float cartY = controlY + btnSize + 15f;
                    float cartH = 48f;
                    RectangleF cartRect = new RectangleF(x, cartY, cardWidth, cartH);

                    using (Brush sBrush = new SolidBrush(shadowColor))
                        g.FillPath(sBrush, RoundedRect(new RectangleF(x, cartY + 4, cardWidth, cartH), 15));

                    using (Brush cBrush = new SolidBrush(btnBaseColor))
                    {
                        g.FillPath(cBrush, RoundedRect(cartRect, 15));
                        using (Pen pen = new Pen(dark ? Color.White : Color.Black, 1))
                            g.DrawPath(pen, RoundedRect(cartRect, 15));

                        string txt = "ADD TO CART";
                        using (Font f = new Font("Segoe UI", 12, FontStyle.Bold))
                        {
                            SizeF sz = g.MeasureString(txt, f);
                            g.DrawString(txt, f, textBrush, x + (cardWidth - sz.Width) / 2, cartY + (cartH - sz.Height) / 2);
                        }
                    }
                }

                // --- DRAW BACK BUTTON (ID 10) ---
                float backBtnSize = 70f;
                float margin = 30f;
                cw = ClientSize.Width;
                RectangleF backRect = new RectangleF(cw - backBtnSize - margin, margin, backBtnSize, backBtnSize);

                // Visual Feedback: Glow Gold if ID 10 is on the table (consistent with Plus/Minus)
                bool backActive = objectList.ContainsKey(10);

                // Use the theme colors you already defined in the function
                using (Brush b = new SolidBrush(backActive ? Color.Gold : btnBaseColor))
                using (Pen p = new Pen(dark ? Color.White : Color.Black, 2))
                {
                    // Draw Shadow (to match the Add to Cart button style)
                    g.FillEllipse(new SolidBrush(shadowColor), backRect.X, backRect.Y + 4, backBtnSize, backBtnSize);

                    // Draw Main Button
                    g.FillEllipse(b, backRect);
                    g.DrawEllipse(p, backRect);

                    using (Font f = new Font("Segoe UI", 12, FontStyle.Bold))
                    {
                        string txt = "BACK";
                        SizeF sz = g.MeasureString(txt, f);
                        // Draw the text using the existing theme textBrush
                        g.DrawString(txt, f, textBrush,
                            backRect.X + (backBtnSize - sz.Width) / 2 + 2,
                            backRect.Y + (backBtnSize - sz.Height) / 2);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Draw Error: " + ex.Message); }
        }
        if (bestsellers == true)
        {
            DrawBestsellersScreen();
        }

        // Draws The Deals Screen
        void DrawDealsScreen()
        {
            try
            {
                // 1. Draw Background
                using (Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png")))
                {
                    Bitmap tempBg = bg;
                    ResizeImage(ref tempBg);
                    g.DrawImage(tempBg, 0, 0);
                }

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;

                // Draw Logo
                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                        g.DrawImage(logo, -100, 0, 400, 200);
                }
                catch { }

                // 2. Data Setup (Items + Discounts)
                string[] dealNames = { "Navy Shirt", "Black Hoodie", "Denim Pants", "Black Jacket", "Pink Hoodie", "Burgundy Shirt", "Black Pants", "Denim Jacket" };
                string[] dealFiles = { "NavyShirt.png", "BlackHoodie.png", "DenimPants.png", "BlackJacket.png", "PinkHoodie.png", "BurgundyShirt.png", "BlackPants.png", "DenimJacket.png" };
                string[] discounts = { "70% OFF", "20% OFF", "50% OFF", "20% OFF", "50% OFF", "70% OFF", "20% OFF", "50% OFF" };

                // 3. Map Counters to an Array for the UI
                int[] counts = { ctNavyShirt, cthoodieBlack, ctDenimPants, ctBlackJacket, cthoodiePink, cthoodieBurgundy, ctBlackPants, ctDenimJacket };

                float cardWidth = cw * 0.20f;
                float cardHeight = ch * 0.35f;
                float spacing = cw * 0.03f;
                float startX = (cw - (4 * cardWidth + 3 * spacing)) / 2;
                float baseY = ch * 0.30f;

                // 4. Colors & Styling
                Color cardColor = dark ? Color.FromArgb(130, 130, 130) : Color.FromArgb(235, 215, 160);
                Color shadowColor = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(180, 160, 110);
                Color barColor = dark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(220, 200, 150);
                Color btnBaseColor = dark ? Color.FromArgb(70, 90, 120) : Color.FromArgb(255, 190, 100);
                Color selectionColor = dark ? Color.White : Color.FromArgb(100, 70, 40);
                Color discountBadgeColor = Color.FromArgb(220, 53, 69); // Bright Red

                Brush textBrush = dark ? Brushes.White : Brushes.Black;
                Pen btnOutline = new Pen(Color.Black, 2);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 5. Draw the 4 Visible Cards
                for (int i = 0; i < 4; i++)
                {
                    int itemIdx = (scrollIndex + i) % dealNames.Length;
                    string currentItemName = dealNames[itemIdx];
                    string currentItemFile = dealFiles[itemIdx];
                    string currentDiscount = discounts[itemIdx];

                    bool isSelected = (hoodieColor == currentItemName);

                    float x = startX + i * (cardWidth + spacing);
                    float y = isSelected ? baseY - 15 : baseY;
                    float shadowOffset = isSelected ? 15 : 10;

                    RectangleF rect = new RectangleF(x, y, cardWidth, cardHeight);
                    RectangleF shadowRect = new RectangleF(x + shadowOffset, y + shadowOffset, cardWidth, cardHeight);

                    // A. Shadow
                    using (GraphicsPath shadowPath = RoundedRect(shadowRect, 25))
                        g.FillPath(new SolidBrush(shadowColor), shadowPath);

                    // B. Selection Border
                    if (isSelected)
                    {
                        using (GraphicsPath borderPath = RoundedRect(new RectangleF(x - 6, y - 6, cardWidth + 12, cardHeight + 12), 32))
                        using (Pen selPen = new Pen(selectionColor, 5))
                            g.DrawPath(selPen, borderPath);
                    }

                    // C. Main Card Body
                    using (GraphicsPath path = RoundedRect(rect, 25))
                    {
                        g.FillPath(new SolidBrush(cardColor), path);
                        using (Pen p = new Pen(isSelected ? selectionColor : Color.White, isSelected ? 3 : 1))
                            g.DrawPath(p, path);
                    }

                    // D. Discount Badge
                    float bW = 100; float bH = 35;
                    RectangleF bRect = new RectangleF(x + cardWidth - bW + 10, y - 15, bW, bH);
                    using (GraphicsPath bPath = RoundedRect(bRect, 12))
                        g.FillPath(new SolidBrush(discountBadgeColor), bPath);

                    using (Font bf = new Font("Arial", 11, FontStyle.Bold))
                        g.DrawString(currentDiscount, bf, Brushes.White, bRect.X + 12, bRect.Y + 8);

                    // E. Product Image
                    try
                    {
                        using (Bitmap imgg = new Bitmap(Path.Combine(themePath, currentItemFile)))
                            g.DrawImage(imgg, x + 10, y + 10, cardWidth - 20, cardHeight - 20);
                    }
                    catch { }

                    // F. Controls UI (Plus / Minus / Count)
                    float controlY = ch * 0.70f;
                    float btnSize = 55f;

                    // Bar Background
                    RectangleF barRect = new RectangleF(x, controlY, cardWidth, btnSize);
                    using (GraphicsPath barPath = RoundedRect(barRect, 30))
                        g.FillPath(new SolidBrush(barColor), barPath);

                    // Detect Physical Objects for Visual Feedback
                    bool plusActive = (isSelected && objectList.ContainsKey(3));
                    bool minusActive = (isSelected && objectList.ContainsKey(4));

                    // Plus Button UI
                    RectangleF pRect = new RectangleF(x, controlY, btnSize, btnSize);
                    using (Brush b = new SolidBrush(plusActive ? Color.Gold : btnBaseColor))
                    {
                        g.FillEllipse(b, pRect);
                        g.DrawEllipse(btnOutline, pRect);
                        g.DrawString("+", new Font("Arial", 22, FontStyle.Bold), Brushes.Black, x + 14, controlY + 11);
                    }

                    // Minus Button UI
                    float mX = x + cardWidth - btnSize;
                    RectangleF mRect = new RectangleF(mX, controlY, btnSize, btnSize);
                    using (Brush b = new SolidBrush(minusActive ? Color.Gold : btnBaseColor))
                    {
                        g.FillEllipse(b, mRect);
                        g.DrawEllipse(btnOutline, mRect);
                        g.DrawString("-", new Font("Arial", 22, FontStyle.Bold), Brushes.Black, mX + 17, controlY + 8);
                    }

                    // Central Quantity Number
                    using (Font f = new Font("Arial", 28, FontStyle.Bold))
                    {
                        string s = counts[itemIdx].ToString();
                        float tx = x + (cardWidth - g.MeasureString(s, f).Width) / 2;
                        g.DrawString(s, f, textBrush, tx, controlY + 4);
                    }

                    // G. Add to Cart Button UI
                    float cartY = controlY + btnSize + 15f;
                    float cartH = 48f;
                    RectangleF cartRect = new RectangleF(x, cartY, cardWidth, cartH);

                    using (Brush sBrush = new SolidBrush(shadowColor))
                        g.FillPath(sBrush, RoundedRect(new RectangleF(x, cartY + 4, cardWidth, cartH), 15));

                    using (Brush cBrush = new SolidBrush(btnBaseColor))
                    {
                        g.FillPath(cBrush, RoundedRect(cartRect, 15));
                        string txt = "ADD TO CART";
                        using (Font f = new Font("Segoe UI", 11, FontStyle.Bold))
                        {
                            SizeF sz = g.MeasureString(txt, f);
                            g.DrawString(txt, f, textBrush, x + (cardWidth - sz.Width) / 2, cartY + (cartH - sz.Height) / 2);
                        }
                    }
                }

                // --- DRAW BACK BUTTON (ID 10) ---
                float backBtnSize = 70f;
                float margin = 30f;
                cw = ClientSize.Width;
                RectangleF backRect = new RectangleF(cw - backBtnSize - margin, margin, backBtnSize, backBtnSize);

                // Visual Feedback: Glow Gold if ID 10 is on the table (consistent with Plus/Minus)
                bool backActive = objectList.ContainsKey(10);

                // Use the theme colors you already defined in the function
                using (Brush b = new SolidBrush(backActive ? Color.Gold : btnBaseColor))
                using (Pen p = new Pen(dark ? Color.White : Color.Black, 2))
                {
                    // Draw Shadow (to match the Add to Cart button style)
                    g.FillEllipse(new SolidBrush(shadowColor), backRect.X, backRect.Y + 4, backBtnSize, backBtnSize);

                    // Draw Main Button
                    g.FillEllipse(b, backRect);
                    g.DrawEllipse(p, backRect);

                    using (Font f = new Font("Segoe UI", 12, FontStyle.Bold))
                    {
                        string txt = "BACK";
                        SizeF sz = g.MeasureString(txt, f);
                        // Draw the text using the existing theme textBrush
                        g.DrawString(txt, f, textBrush,
                            backRect.X + (backBtnSize - sz.Width) / 2 + 2,
                            backRect.Y + (backBtnSize - sz.Height) / 2);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Draw Deals Error: " + ex.Message); }
        }
        if (deals == true)
        {
            DrawDealsScreen();
        }


        // Draws The Outfit Builder Screen
        // Draws The Outfit Builder Screen
        void DrawOutfitBuilderScreen()
        {
            try
            {
                // 1. Background & Logo
                using (Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png")))
                {
                    Bitmap tempBg = bg; ResizeImage(ref tempBg); g.DrawImage(tempBg, 0, 0);
                }
                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                        g.DrawImage(logo, -100, 0, 400, 200);
                }
                catch { }

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Theme Colors
                Color cardCol = dark ? Color.FromArgb(130, 130, 130) : Color.FromArgb(235, 215, 160);
                Color shadowCol = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(180, 160, 110);
                Color btnCol = dark ? Color.FromArgb(70, 90, 120) : Color.FromArgb(255, 190, 100);
                Color selectionColor = dark ? Color.White : Color.FromArgb(100, 70, 40);
                Color barColor = dark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(220, 200, 150);
                Brush textBrush = dark ? Brushes.White : Brushes.Black;
                Pen btnOutline = new Pen(Color.Black, 2);
                Pen selectionPen = new Pen(selectionColor, 3);

                // === LEFT PART: HUGE OUTFIT CARD (2/3 of screen) ===
                float leftWidth = cw * 0.65f;
                float previewCardW = leftWidth * 0.75f;
                float previewCardH = ch * 0.55f;
                float previewCardX = (leftWidth - previewCardW) / 2 + 50;
                float previewCardY = 100;
                RectangleF previewRect = new RectangleF(previewCardX, previewCardY, previewCardW, previewCardH);

                // Shadow
                g.FillPath(new SolidBrush(shadowCol), RoundedRect(new RectangleF(previewRect.X + 12, previewRect.Y + 12, previewRect.Width, previewRect.Height), 40));

                // Main Card
                g.FillPath(new SolidBrush(cardCol), RoundedRect(previewRect, 40));
                g.DrawPath(new Pen(Color.White, 2), RoundedRect(previewRect, 40));

                // Draw TOP (Shirt/Hoodie/Jacket)
                if (!string.IsNullOrEmpty(currentTop))
                {
                    try
                    {
                        string topPath = Path.Combine(themePath, currentTop + ".png");
                        if (File.Exists(topPath))
                        {
                            using (Bitmap img = new Bitmap(topPath))
                                g.DrawImage(img, previewRect.X + 50, previewRect.Y - 10, previewRect.Width - 100, previewRect.Height * 0.70f);
                        }
                    }
                    catch { }
                    g.DrawString($"Top: {currentTop}", new Font("Segoe UI", 10, FontStyle.Bold), textBrush, previewRect.X + 15, previewRect.Y + 10);
                }

                // Draw BOTTOM (Pants/Shorts)
                if (!string.IsNullOrEmpty(currentBottom))
                {
                    try
                    {
                        string bottomPath = Path.Combine(themePath, currentBottom + ".png");
                        if (File.Exists(bottomPath))
                        {
                            using (Bitmap img = new Bitmap(bottomPath))
                                g.DrawImage(img, previewRect.X + 80, previewRect.Y - 10 + (previewRect.Height * 0.38f), previewRect.Width - 160, previewRect.Height * 0.70f);
                        }
                    }
                    catch { }
                    g.DrawString($"Bottom: {currentBottom}", new Font("Segoe UI", 10, FontStyle.Bold), textBrush, previewRect.X + 15, previewRect.Bottom - 30);
                }

                // === ADD TO CART BUTTON (ID 5) ===
                bool addCartActive = HasObjectWithSymbolID(5);
                float addBtnW = 200f;
                float addBtnH = 55f;
                RectangleF addCartRect = new RectangleF(previewRect.X + 250 + (previewRect.Width - addBtnW) / 2, previewRect.Bottom + 20, addBtnW, addBtnH);

                g.FillPath(new SolidBrush(shadowCol), RoundedRect(new RectangleF(addCartRect.X, addCartRect.Y + 5, addCartRect.Width, addCartRect.Height), 20));
                g.FillPath(new SolidBrush(addCartActive ? btnCol : btnCol), RoundedRect(addCartRect, 20));
                g.DrawPath(new Pen(dark ? Color.White : Color.Black, 2), RoundedRect(addCartRect, 20));

                using (Font btnFont = new Font("Segoe UI", 13, FontStyle.Bold))
                {
                    string btnTxt = "ADD TO CART";
                    SizeF btnSz = g.MeasureString(btnTxt, btnFont);
                    g.DrawString(btnTxt, btnFont, Brushes.Black, addCartRect.X + (addCartRect.Width - btnSz.Width) / 2, addCartRect.Y + (addCartRect.Height - btnSz.Height) / 2);
                }

                // === CART PREVIEW (Bottom Left - Like Checkout Page) ===
                // Only show cart when there are items in it
                int totalCartItems = 0;
                List<string> cartItemsList = new List<string>();
                foreach (var entry in cart)
                {
                    if (entry.Value > 0)
                    {
                        totalCartItems += entry.Value;
                        cartItemsList.Add(entry.Key);
                    }
                }

                // Auto-select first item in cart if hoodieColor is empty or not in cart
                if (cartItemsList.Count > 0)
                {
                    if (string.IsNullOrEmpty(hoodieColor) || !cartItemsList.Contains(hoodieColor))
                    {
                        hoodieColor = cartItemsList[0];
                    }
                }

                if (cartItemsList.Count > 0)
                {
                    float cartPreviewX = 30;
                    float cartPreviewY = ch - 320;
                    float cartPreviewW = leftWidth * 0.60f;
                    float cartPreviewH = 300;

                    RectangleF cartPreviewRect = new RectangleF(cartPreviewX, cartPreviewY, cartPreviewW, cartPreviewH);
                    g.FillPath(new SolidBrush(shadowCol), RoundedRect(new RectangleF(cartPreviewX + 8, cartPreviewY + 8, cartPreviewW, cartPreviewH), 25));
                    g.FillPath(new SolidBrush(cardCol), RoundedRect(cartPreviewRect, 25));
                    g.DrawPath(new Pen(Color.White, 2), RoundedRect(cartPreviewRect, 25));

                    // Cart Title with item count
                    g.DrawString($"Cart ({cartItemsList.Count} items)", new Font("Segoe UI", 16, FontStyle.Bold), textBrush, cartPreviewX + 20, cartPreviewY + 12);

                    // Scroll arrows if more than 4 items
                    if (cartItemsList.Count > 4)
                    {
                        g.DrawString("▲", new Font("Arial", 14, FontStyle.Bold),
                            new SolidBrush(cartScrollIndex > 0 ? selectionColor : Color.Gray),
                            cartPreviewX + cartPreviewW - 35, cartPreviewY + 10);
                        g.DrawString("▼", new Font("Arial", 14, FontStyle.Bold),
                            new SolidBrush(cartScrollIndex < cartItemsList.Count - 4 ? selectionColor : Color.Gray),
                            cartPreviewX + cartPreviewW - 35, cartPreviewRect.Bottom - 45);
                    }

                    float itemY = cartPreviewY + 45;
                    int displayCount = 0;
                    int maxDisplay = 4;

                    // Ensure scroll index is valid
                    if (cartScrollIndex >= cartItemsList.Count) cartScrollIndex = 0;
                    if (cartScrollIndex < 0) cartScrollIndex = 0;

                    using (Font itemFont = new Font("Segoe UI", 11, FontStyle.Bold))
                    using (Font symbolFont = new Font("Arial", 16, FontStyle.Bold))
                    {
                        // Display items starting from cartScrollIndex
                        for (int idx = cartScrollIndex; idx < cartItemsList.Count && displayCount < maxDisplay; idx++)
                        {
                            string itemName = cartItemsList[idx];
                            int qty = cart[itemName];
                            bool isSelectedItem = (hoodieColor == itemName);

                            // Selection highlight - ALWAYS show on selected item
                            if (isSelectedItem)
                            {
                                RectangleF selRect = new RectangleF(cartPreviewX + 10, itemY - 3, cartPreviewW - 20, 48);
                                g.DrawPath(selectionPen, RoundedRect(selRect, 12));
                            }

                            // Item thumbnail
                            try
                            {
                                using (Bitmap itemImg = new Bitmap(Path.Combine(themePath, itemName + ".png")))
                                    g.DrawImage(itemImg, cartPreviewX + 18, itemY + 2, 38, 38);
                            }
                            catch { }

                            // Item name
                            g.DrawString(itemName, itemFont, textBrush, cartPreviewX + 65, itemY + 10);

                            // Quantity Pill
                            float pillX = cartPreviewX + cartPreviewW - 150;
                            RectangleF pillRect = new RectangleF(pillX, itemY + 5, 95, 32);
                            g.FillPath(new SolidBrush(barColor), RoundedRect(pillRect, 16));

                            // Minus button
                            g.DrawString("-", symbolFont, textBrush, pillX + 8, itemY + 6);

                            // Quantity number
                            string qtyStr = qty.ToString();
                            SizeF qtySize = g.MeasureString(qtyStr, itemFont);
                            g.DrawString(qtyStr, itemFont, textBrush, pillX + 47 - qtySize.Width / 2, itemY + 10);

                            // Plus button
                            g.DrawString("+", symbolFont, textBrush, pillX + 70, itemY + 6);

                            // Delete button
                            try
                            {
                                using (Bitmap delImg = new Bitmap(Path.Combine(themePath, "Delete.png")))
                                    g.DrawImage(delImg, pillX + 100, itemY + 5, 28, 28);
                            }
                            catch { }

                            itemY += 52;
                            displayCount++;
                        }
                    }

                    // Instructions at bottom
                    g.DrawString("ID 2: select | ID 3/4: qty | ID 16: delete | Rotate ID 2: scroll", new Font("Segoe UI", 8), textBrush, cartPreviewX + 10, cartPreviewRect.Bottom - 22);
                }

                // === RIGHT PART: CATEGORY CARDS ===
                float rightX = leftWidth + 15;
                float catW = cw * 0.30f;
                float catH = ch * 0.14f;
                float spacing = 12;
                int[] scrollerIDs = { 11, 12, 13, 14, 15 };
                string[] catNames = { "Shirts", "Hoodies", "Jackets", "Pants", "Shorts" };
                float y = 20;
                for (int i = 0; i < 5; i++)
                {
                    if (i == 0)
                    {
                        y += 80 + i * (catH + spacing);
                    }
                    else
                    {
                        y = 100 + i * (catH + spacing);
                    }
                    bool isScrolling = HasObjectWithSymbolID(scrollerIDs[i]);

                    int itemIdx = Math.Abs(scrollIndices[i]) % items[i].Length;
                    string displayedItem = items[i][itemIdx];

                    bool isWorn = (currentTop == displayedItem || currentBottom == displayedItem);
                    bool isSelected = isScrolling;

                    float cardY = isSelected ? y - 10 : y;
                    float shadowOffset = isSelected ? 14 : 8;

                    RectangleF catRect = new RectangleF(rightX, cardY, catW, catH);
                    RectangleF shadowRect = new RectangleF(rightX + shadowOffset, cardY + shadowOffset, catW, catH);

                    // Shadow
                    g.FillPath(new SolidBrush(shadowCol), RoundedRect(shadowRect, 20));

                    // Selection Border (like home page)
                    if (isSelected)
                    {
                        RectangleF borderRect = new RectangleF(rightX - 6, cardY - 6, catW + 12, catH + 12);
                        g.DrawPath(new Pen(selectionColor, 5), RoundedRect(borderRect, 26));
                    }

                    // Worn indicator (gold border)
                    if (isWorn)
                    {
                        g.DrawPath(new Pen(Color.Gold, 4), RoundedRect(catRect, 20));
                    }

                    // Main Card
                    Color cardFill = isScrolling ? Color.FromArgb(255, 235, 180) : cardCol;
                    g.FillPath(new SolidBrush(cardFill), RoundedRect(catRect, 20));
                    g.DrawPath(new Pen(isSelected ? selectionColor : Color.White, isSelected ? 2 : 1), RoundedRect(catRect, 20));

                    // Category Label
                    g.DrawString(catNames[i], new Font("Segoe UI", 12, FontStyle.Bold), textBrush, rightX + 12, cardY + 10);

                    // Item Image
                    try
                    {
                        using (Bitmap img = new Bitmap(Path.Combine(themePath, displayedItem + ".png")))
                            g.DrawImage(img, rightX + catW - catH - 5, cardY + 8, catH - 16, catH - 16);
                    }
                    catch { }

                    // Item Name
                    g.DrawString(displayedItem, new Font("Segoe UI", 10), textBrush, rightX + 12, cardY + catH - 28);

                }

                // === BACK BUTTON (ID 10) ===
                float backBtnSize = 60f;
                float margin = 25f;
                RectangleF backRect = new RectangleF(cw - backBtnSize - margin, margin, backBtnSize, backBtnSize);
                bool backActive = HasObjectWithSymbolID(10);

                g.FillEllipse(new SolidBrush(shadowCol), backRect.X, backRect.Y + 4, backBtnSize, backBtnSize);
                g.FillEllipse(new SolidBrush(backActive ? Color.Gold : btnCol), backRect);
                g.DrawEllipse(new Pen(dark ? Color.White : Color.Black, 2), backRect);

                using (Font backFont = new Font("Segoe UI", 11, FontStyle.Bold))
                {
                    string backTxt = "BACK";
                    SizeF backSz = g.MeasureString(backTxt, backFont);
                    g.DrawString(backTxt, backFont, textBrush, backRect.X + (backBtnSize - backSz.Width) / 2 + 2, backRect.Y + (backBtnSize - backSz.Height) / 2);
                }

            }
            catch (Exception ex) { Console.WriteLine("OutfitBuilder Error: " + ex.Message); }
        }
        if (outfitbuilder == true)
        {
            DrawOutfitBuilderScreen();
        }

        /// Draws The Checkout Screen
        void DrawCheckoutScreen()
        {
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

                float cardW = cw * 0.60f; // Slightly wider to fit longer names
                float cardH = ch * 0.75f;
                float cardX = (cw - cardW) / 2;
                float cardY = (ch - cardH) / 2 + 40;

                // Theme Colors
                Color cardCol = dark ? Color.FromArgb(130, 130, 130) : Color.FromArgb(235, 215, 160);
                Color shadowCol = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(180, 160, 110);
                Color btnCol = dark ? Color.FromArgb(70, 90, 120) : Color.FromArgb(255, 190, 100);
                Brush textBrush = dark ? Brushes.White : Brushes.Black;
                Pen selectionPen = new Pen(dark ? Color.White : Color.SaddleBrown, 3);

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw Card Shadow & Body
                g.FillPath(new SolidBrush(shadowCol), RoundedRect(new RectangleF(cardX + 10, cardY + 10, cardW, cardH), 30));
                g.FillPath(new SolidBrush(cardCol), RoundedRect(new RectangleF(cardX, cardY, cardW, cardH), 30));

                using (Font titleFont = new Font("Segoe UI Semibold", 24))
                using (Font itemFont = new Font("Segoe UI", 12, FontStyle.Bold))
                using (Font symbolFont = new Font("Arial", 18, FontStyle.Bold))
                {
                    if (totalItems == 0)
                    {
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
                        g.DrawString("Order Summary", titleFont, textBrush, cardX + 50, cardY + 30);

                        float itemY = cardY + 90;
                        double subtotal = 0;

                        foreach (var entry in cart)
                        {
                            string itemName = entry.Key;
                            int qty = entry.Value;

                            if (qty > 0)
                            {
                                // Selection highlight for current item
                                if (hoodieColor == itemName)
                                {
                                    RectangleF selRect = new RectangleF(cardX + 25, itemY - 5, cardW - 50, 65);
                                    g.DrawPath(selectionPen, RoundedRect(selRect, 15));
                                }

                                // Draw Product Thumbnail - try multiple approaches
                                bool imageDrawn = false;
                                try
                                {
                                    // First, try loading directly with item name (for outfit builder items like "WhiteShirt")
                                    string directPath = Path.Combine(themePath, itemName + ".png");
                                    if (File.Exists(directPath))
                                    {
                                        using (Bitmap hImg = new Bitmap(directPath))
                                        {
                                            g.DrawImage(hImg, cardX + 40, itemY, 50, 50);
                                            imageDrawn = true;
                                        }
                                    }
                                }
                                catch { }

                                // If direct load failed, try the itemMap for legacy items
                                if (!imageDrawn)
                                {
                                    try
                                    {
                                        // Map for legacy item names to file names
                                        var itemMap = new Dictionary<string, string> {
                                            { "Black", "BlackHoodie.png" }, { "Grey", "GreyHoodie.png" },
                                            { "Burgundy", "BurgundyHoodie.png" }, { "Pink", "PinkHoodie.png" },
                                            { "Navy Shirt", "NavyShirt.png" }, { "Black Hoodie", "BlackHoodie.png" },
                                            { "Denim Pants", "DenimPants.png" }, { "Black Jacket", "BlackJacket.png" },
                                            { "Pink Hoodie", "PinkHoodie.png" }, { "Burgundy Shirt", "BurgundyShirt.png" },
                                            { "Black Pants", "BlackPants.png" }, { "Denim Jacket", "DenimJacket.png" }
                                        };

                                        if (itemMap.ContainsKey(itemName))
                                        {
                                            using (Bitmap hImg = new Bitmap(Path.Combine(themePath, itemMap[itemName])))
                                                g.DrawImage(hImg, cardX + 40, itemY, 50, 50);
                                        }
                                    }
                                    catch { }
                                }

                                // Item Name and Qty
                                g.DrawString(itemName, itemFont, textBrush, cardX + 100, itemY + 12);

                                // Qty Pill UI
                                float recX = cardX + cardW - 280;
                                RectangleF pillRect = new RectangleF(recX, itemY + 5, 100, 35);
                                using (Brush b = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                                    g.FillPath(b, RoundedRect(pillRect, 18));

                                g.DrawString("-", symbolFont, textBrush, recX + 10, itemY + 8);
                                g.DrawString(qty.ToString(), itemFont, textBrush, recX + 40, itemY + 10);
                                g.DrawString("+", symbolFont, textBrush, recX + 75, itemY + 8);

                                // Delete button
                                try
                                {
                                    using (Bitmap delImg = new Bitmap(Path.Combine(themePath, "Delete.png")))
                                        g.DrawImage(delImg, recX + 110, itemY + 5, 30, 30);
                                }
                                catch { }

                                // Price Calculation
                                float price = qty * 50.00f; // Simplified: 50 per item
                                subtotal += price;
                                string pStr = $"{price:F2}$";
                                g.DrawString(pStr, itemFont, textBrush, cardX + cardW - 50 - g.MeasureString(pStr, itemFont).Width, itemY + 12);

                                itemY += 65; // Spacing for next item
                            }
                        }

                        // Summary Totals
                        itemY = cardY + cardH - 220;
                        double service = subtotal * 0.12;
                        double tax = subtotal * 0.14;
                        double totalVal = subtotal + service + tax;

                        string[,] summary = { { "Subtotal", $"{subtotal:F2}$" }, { "Service (12%)", $"{service:F2}$" }, { "TAX (14%)", $"{tax:F2}$" } };
                        for (int i = 0; i < 3; i++)
                        {
                            g.DrawString(summary[i, 0], itemFont, textBrush, cardX + 50, itemY + (i * 25));
                            g.DrawString(summary[i, 1], itemFont, textBrush, cardX + cardW - 60 - g.MeasureString(summary[i, 1], itemFont).Width, itemY + (i * 25));
                        }

                        // Divider and Grand Total
                        float footerY = cardY + cardH - 95;
                        g.DrawLine(new Pen(textBrush, 2), cardX + 50, footerY - 10, cardX + cardW - 50, footerY - 10);
                        g.DrawString("Total Payment", itemFont, textBrush, cardX + 50, footerY + 15);
                        g.DrawString($"{totalVal:F2}$", titleFont, textBrush, cardX + 185, footerY + 5);

                        // Checkout Button
                        float btnW = 160; float btnH = 55;
                        float btnX = cardX + cardW - btnW - 40;
                        RectangleF btnRect = new RectangleF(btnX, footerY + 5, btnW, btnH);
                        using (Brush b = new SolidBrush(btnCol))
                            g.FillPath(b, RoundedRect(btnRect, 15));

                        g.DrawPath(new Pen(Color.Black, 2), RoundedRect(btnRect, 15));
                        string checkTxt = "CHECKOUT";
                        SizeF sSize = g.MeasureString(checkTxt, itemFont);
                        g.DrawString(checkTxt, itemFont, dark ? Brushes.White : Brushes.Black, btnX + (btnW - sSize.Width) / 2, footerY + 5 + (btnH - sSize.Height) / 2);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Checkout Draw Error: " + ex.Message); }
        }
        if (checkout == true)
        {
            DrawCheckoutScreen();
        }

        void DrawThankYouScreen()
        {
            Bitmap img = new Bitmap(Path.Combine(themePath, "ThankYou.png"));
            ResizeImage(ref img);
            Display_Current_Page(img);
        }
        if (thankyou == true)
        {
            DrawThankYouScreen();
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
                    if (tobj.SymbolID == 2 && clothes)
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
                    ///

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

                    // === CHECKOUT DELETE ITEM (ID 16) ===
                    if (tobj.SymbolID == 16 && checkout)
                    {
                        if ((DateTime.Now - hoodieSwitch).TotalMilliseconds > 500)
                        {
                            if (!string.IsNullOrEmpty(hoodieColor) && cart.ContainsKey(hoodieColor) && cart[hoodieColor] > 0)
                            {
                                Console.WriteLine($"CHECKOUT: Deleting {hoodieColor} from cart");
                                cart[hoodieColor] = 0;

                                // Select another item in cart if available
                                hoodieColor = "";
                                foreach (var entry in cart)
                                {
                                    if (entry.Value > 0)
                                    {
                                        hoodieColor = entry.Key;
                                        break;
                                    }
                                }
                            }
                            hoodieSwitch = DateTime.Now;
                        }
                    }

                    /// Handles the logic for adding and removing hoodies from the cart based on the rotation of the object with SymbolID 4, allowing users to adjust the quantity of the selected hoodie color before proceeding to checkout.
                    if (tobj.SymbolID == 3 && (clothes || bestsellers || deals))
                    {
                        if ((DateTime.Now - hoodieCount).TotalMilliseconds > 400)
                        {
                            // 1. Original / Shared Colors
                            if (hoodieColor == "Black" || hoodieColor == "Black Hoodie") cthoodieBlack++;
                            else if (hoodieColor == "Pink" || hoodieColor == "Pink Hoodie") cthoodiePink++;
                            else if (hoodieColor == "Burgundy" || hoodieColor == "Burgundy Shirt") cthoodieBurgundy++;
                            else if (hoodieColor == "Grey") cthoodieGrey++;

                            // 2. New OutfitBuilder & Deals Specifics
                            else if (hoodieColor == "Navy Shirt") ctNavyShirt++;
                            else if (hoodieColor == "Denim Pants") ctDenimPants++;
                            else if (hoodieColor == "Black Jacket") ctBlackJacket++;
                            else if (hoodieColor == "Black Pants") ctBlackPants++;
                            else if (hoodieColor == "Denim Jacket") ctDenimJacket++;

                            // 3. Dynamic OutfitBuilder items (if not explicitly handled above)
                            // This ensures the numbers update even for items without dedicated variables
                            if (!cart.ContainsKey(hoodieColor)) cart[hoodieColor] = 0;

                            hoodieCount = DateTime.Now;
                        }
                    }

                    /// Handles Hoodie Count Decreasing, ensuring it doesn't go below 0 and only updates once per rotation using a cooldown.
                    // MINUS BUTTON (ID 4)
                    if (tobj.SymbolID == 4 && (clothes || bestsellers || deals))
                    {
                        if ((DateTime.Now - hoodieCount).TotalMilliseconds > 400)
                        {
                            if (hoodieColor == "Black" || hoodieColor == "Black Hoodie") { if (cthoodieBlack > 0) cthoodieBlack--; }
                            else if (hoodieColor == "Grey") { if (cthoodieGrey > 0) cthoodieGrey--; }
                            else if (hoodieColor == "Burgundy" || hoodieColor == "Burgundy Shirt") { if (cthoodieBurgundy > 0) cthoodieBurgundy--; }
                            else if (hoodieColor == "Pink" || hoodieColor == "Pink Hoodie") { if (cthoodiePink > 0) cthoodiePink--; }
                            else if (hoodieColor == "Navy Shirt") { if (ctNavyShirt > 0) ctNavyShirt--; }
                            else if (hoodieColor == "Denim Pants") { if (ctDenimPants > 0) ctDenimPants--; }
                            else if (hoodieColor == "Black Jacket") { if (ctBlackJacket > 0) ctBlackJacket--; }
                            else if (hoodieColor == "Black Pants") { if (ctBlackPants > 0) ctBlackPants--; }
                            else if (hoodieColor == "Denim Jacket") { if (ctDenimJacket > 0) ctDenimJacket--; }

                            hoodieCount = DateTime.Now;
                        }
                    }

                    /// Handles "ADD TO CART" button
                    if (tobj.SymbolID == 5 && (clothes || bestsellers || deals || outfitbuilder))
                    {
                        if ((DateTime.Now - hoodieSwitch).TotalMilliseconds > 1500)
                        {
                            if (outfitbuilder)
                            {
                                // Add the visible outfit pieces directly to the cart dictionary
                                if (!string.IsNullOrEmpty(currentTop))
                                {
                                    if (!cart.ContainsKey(currentTop)) cart[currentTop] = 0;
                                    cart[currentTop]++;
                                }
                                if (!string.IsNullOrEmpty(currentBottom))
                                {
                                    if (!cart.ContainsKey(currentBottom)) cart[currentBottom] = 0;
                                    cart[currentBottom]++;
                                }
                            }
                            else // Logic for Clothes, Bestsellers, Deals
                            {
                                int amount = 0;
                                if (hoodieColor == "Black" || hoodieColor == "Black Hoodie") { amount = cthoodieBlack; cthoodieBlack = 0; }
                                else if (hoodieColor == "Grey") { amount = cthoodieGrey; cthoodieGrey = 0; }
                                else if (hoodieColor == "Burgundy" || hoodieColor == "Burgundy Shirt") { amount = cthoodieBurgundy; cthoodieBurgundy = 0; }
                                else if (hoodieColor == "Pink" || hoodieColor == "Pink Hoodie") { amount = cthoodiePink; cthoodiePink = 0; }
                                else if (hoodieColor == "Navy Shirt") { amount = ctNavyShirt; ctNavyShirt = 0; }
                                else if (hoodieColor == "Denim Pants") { amount = ctDenimPants; ctDenimPants = 0; }
                                else if (hoodieColor == "Black Jacket") { amount = ctBlackJacket; ctBlackJacket = 0; }
                                else if (hoodieColor == "Black Pants") { amount = ctBlackPants; ctBlackPants = 0; }
                                else if (hoodieColor == "Denim Jacket") { amount = ctDenimJacket; ctDenimJacket = 0; }

                                if (amount > 0)
                                {
                                    if (!cart.ContainsKey(hoodieColor)) cart[hoodieColor] = 0;
                                    cart[hoodieColor] += amount;
                                }
                            }
                            hoodieSwitch = DateTime.Now;
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
                    int[] scrollerIDs = { 11, 12, 13, 14, 15 };
                    // === OUTFIT BUILDER SCROLLING (IDs 11-15) ===
                    // ========== OUTFIT BUILDER COMPLETE LOGIC ==========
                    if (outfitbuilder)
                    {

                        // --- SCROLLING LOGIC (IDs 11-15) ---
                        for (int i = 0; i < scrollerIDs.Length; i++)
                        {
                            if (tobj.SymbolID == scrollerIDs[i])
                            {
                                if ((DateTime.Now - lastScrollTime).TotalMilliseconds > 350)
                                {
                                    int oldIdx = scrollIndices[i];

                                    // Rotate Right
                                    if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 120)
                                    {
                                        scrollIndices[i] = (scrollIndices[i] + 1) % items[i].Length;
                                    }
                                    // Rotate Left
                                    else if (tobj.AngleDegrees > 240 && tobj.AngleDegrees < 340)
                                    {
                                        scrollIndices[i] = (scrollIndices[i] - 1 + items[i].Length) % items[i].Length;
                                    }

                                    if (oldIdx != scrollIndices[i])
                                    {
                                        // Update hoodieColor to current scrolled item
                                        hoodieColor = items[i][scrollIndices[i]];
                                        lastScrollTime = DateTime.Now;
                                    }
                                }
                            }
                        }

                        // --- SELECTION LOGIC (ID 8) - THIS IS THE KEY FIX ---
                        //if (tobj.SymbolID == 8)
                        {
                            if ((DateTime.Now - lastOutfitSelectTime).TotalMilliseconds > 600)
                            {
                                // Check each scroller to see which category is active
                                for (int i = 0; i < scrollerIDs.Length; i++)
                                {
                                    // Check if this scroller ID is currently on the table
                                    bool scrollerPresent = false;
                                    foreach (TuioObject checkObj in objectList.Values)
                                    {
                                        if (checkObj.SymbolID == scrollerIDs[i])
                                        {
                                            scrollerPresent = true;
                                            break;
                                        }
                                    }

                                    if (scrollerPresent)
                                    {
                                        // Get the item at the current scroll index
                                        int itemIdx = Math.Abs(scrollIndices[i]) % items[i].Length;
                                        string selectedItem = items[i][itemIdx];

                                        // TOPS: Shirts (0), Hoodies (1), Jackets (2)
                                        if (i <= 2)
                                        {
                                            currentTop = selectedItem;
                                            Console.WriteLine($"TOP changed to: {currentTop}");
                                        }
                                        // BOTTOMS: Pants (3), Shorts (4)
                                        else
                                        {
                                            currentBottom = selectedItem;
                                            Console.WriteLine($"BOTTOM changed to: {currentBottom}");
                                        }

                                        hoodieColor = selectedItem;
                                        lastOutfitSelectTime = DateTime.Now;
                                        break; // Only select one category at a time
                                    }
                                }
                            }
                        }

                        // --- QUANTITY PLUS (ID 3) ---
                        if (tobj.SymbolID == 3)
                        {
                            if ((DateTime.Now - hoodieCount).TotalMilliseconds > 400)
                            {
                                if (!string.IsNullOrEmpty(hoodieColor))
                                {
                                    if (!cart.ContainsKey(hoodieColor)) cart[hoodieColor] = 0;
                                    cart[hoodieColor]++;
                                    hoodieCount = DateTime.Now;
                                }
                            }
                        }

                        // --- QUANTITY MINUS (ID 4) ---
                        if (tobj.SymbolID == 4)
                        {
                            if ((DateTime.Now - hoodieCount).TotalMilliseconds > 400)
                            {
                                if (!string.IsNullOrEmpty(hoodieColor) && cart.ContainsKey(hoodieColor) && cart[hoodieColor] > 0)
                                {
                                    cart[hoodieColor]--;
                                    hoodieCount = DateTime.Now;
                                }
                            }
                        }

                        // --- ADD TO CART (ID 5) ---
                        if (tobj.SymbolID == 5)
                        {
                            if ((DateTime.Now - hoodieSwitch).TotalMilliseconds > 1000)
                            {
                                if (!string.IsNullOrEmpty(currentTop))
                                {
                                    if (!cart.ContainsKey(currentTop)) cart[currentTop] = 0;
                                    cart[currentTop]++;
                                }
                                if (!string.IsNullOrEmpty(currentBottom))
                                {
                                    if (!cart.ContainsKey(currentBottom)) cart[currentBottom] = 0;
                                    cart[currentBottom]++;
                                }
                                hoodieSwitch = DateTime.Now;
                            }
                        }

                        // --- SELECTION CYCLING (ID 2) - Toggle between top/bottom for quantity ---
                        if (tobj.SymbolID == 2)
                        {
                            if ((DateTime.Now - lastSelectionTime).TotalMilliseconds > 500)
                            {
                                if (hoodieColor == currentTop && !string.IsNullOrEmpty(currentBottom))
                                    hoodieColor = currentBottom;
                                else if (!string.IsNullOrEmpty(currentTop))
                                    hoodieColor = currentTop;

                                lastSelectionTime = DateTime.Now;
                            }
                        }
                    }
                    // ========== END OUTFIT BUILDER LOGIC ==========

                    // === OUTFIT BUILDER SELECTION (ID 8) ===
                    if (tobj.SymbolID == 8)
                    {
                        // Navigation from Home to sub-pages
                        if (home)
                        {
                            home = false;
                            scrollIndex = 0;

                            if (selectedHomeCard == 0) { bestsellers = true; hoodieColor = bestNames[0]; }
                            else if (selectedHomeCard == 1) { deals = true; hoodieColor = dealNames[0]; }
                            else if (selectedHomeCard == 2)
                            {
                                outfitbuilder = true;
                                // Set default outfit when entering
                                //currentTop = "WhiteShirt";
                                //currentBottom = "BlackPants";
                                hoodieColor = currentTop;
                            }
                        }
                        // Selection within Outfit Builder
                        else if (outfitbuilder)
                        {
                            if ((DateTime.Now - lastOutfitSelectTime).TotalMilliseconds > 600)
                            {

                                for (int i = 0; i < scrollerIDs.Length; i++)
                                {
                                    // FIX: Check by SymbolID, not by dictionary key!
                                    bool scrollerPresent = false;
                                    foreach (TuioObject checkObj in objectList.Values)
                                    {
                                        if (checkObj.SymbolID == scrollerIDs[i])
                                        {
                                            scrollerPresent = true;
                                            break;
                                        }
                                    }

                                    if (scrollerPresent)
                                    {
                                        int itemIdx = Math.Abs(scrollIndices[i]) % items[i].Length;
                                        string selectedItem = items[i][itemIdx];

                                        // TOPS: Shirts (0), Hoodies (1), Jackets (2)
                                        if (i <= 2)
                                        {
                                            currentTop = selectedItem;
                                        }
                                        // BOTTOMS: Pants (3), Shorts (4)
                                        else
                                        {
                                            currentBottom = selectedItem;
                                        }

                                        hoodieColor = selectedItem;
                                        lastOutfitSelectTime = DateTime.Now;
                                        Console.WriteLine($"SELECTED: {selectedItem} | Top={currentTop} | Bottom={currentBottom}");
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // === OUTFIT BUILDER QUANTITY CONTROL (ID 3 & 4) ===
                    if ((tobj.SymbolID == 3 || tobj.SymbolID == 4) && outfitbuilder)
                    {
                        if ((DateTime.Now - hoodieCount).TotalMilliseconds > 400)
                        {
                            // Check if hoodieColor is set and exists in cart
                            if (!string.IsNullOrEmpty(hoodieColor) && cart.ContainsKey(hoodieColor))
                            {
                                // Plus button (ID 3) - always allow adding
                                if (tobj.SymbolID == 3)
                                {
                                    cart[hoodieColor]++;
                                    Console.WriteLine($"OUTFIT BUILDER: Added 1 to {hoodieColor}, total: {cart[hoodieColor]}");
                                    hoodieCount = DateTime.Now;
                                }
                                // Minus button (ID 4) - only if quantity > 0
                                else if (tobj.SymbolID == 4 && cart[hoodieColor] > 0)
                                {
                                    cart[hoodieColor]--;
                                    Console.WriteLine($"OUTFIT BUILDER: Removed 1 from {hoodieColor}, total: {cart[hoodieColor]}");
                                    hoodieCount = DateTime.Now;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"OUTFIT BUILDER: hoodieColor={hoodieColor} not in cart or empty");
                            }
                        }
                    }

                    // === OUTFIT BUILDER ADD TO CART (ID 5) ===
                    if (tobj.SymbolID == 5 && outfitbuilder)
                    {
                        if ((DateTime.Now - hoodieSwitch).TotalMilliseconds > 1000)
                        {
                            // Add current TOP to cart
                            if (!string.IsNullOrEmpty(currentTop))
                            {
                                if (!cart.ContainsKey(currentTop)) cart[currentTop] = 0;
                                cart[currentTop]++;
                                Console.WriteLine($"Added {currentTop} to cart");
                                // Set hoodieColor to the added item for selection
                                hoodieColor = currentTop;
                            }

                            // Add current BOTTOM to cart
                            if (!string.IsNullOrEmpty(currentBottom))
                            {
                                if (!cart.ContainsKey(currentBottom)) cart[currentBottom] = 0;
                                cart[currentBottom]++;
                                Console.WriteLine($"Added {currentBottom} to cart");
                            }

                            hoodieSwitch = DateTime.Now;
                        }
                    }

                    // === OUTFIT BUILDER DELETE ITEM (ID 16) ===
                    if (tobj.SymbolID == 16 && outfitbuilder)
                    {
                        if ((DateTime.Now - hoodieSwitch).TotalMilliseconds > 500)
                        {
                            if (!string.IsNullOrEmpty(hoodieColor) && cart.ContainsKey(hoodieColor) && cart[hoodieColor] > 0)
                            {
                                Console.WriteLine($"OUTFIT BUILDER: Deleting {hoodieColor} from cart and outfit");

                                // Remove from outfit display so it won't be added again
                                if (currentTop == hoodieColor)
                                {
                                    currentTop = "";
                                    Console.WriteLine($"Removed {hoodieColor} from currentTop");
                                }
                                if (currentBottom == hoodieColor)
                                {
                                    currentBottom = "";
                                    Console.WriteLine($"Removed {hoodieColor} from currentBottom");
                                }

                                cart[hoodieColor] = 0;

                                // Select another item in cart if available
                                hoodieColor = "";
                                foreach (var entry in cart)
                                {
                                    if (entry.Value > 0)
                                    {
                                        hoodieColor = entry.Key;
                                        break;
                                    }
                                }
                            }
                            hoodieSwitch = DateTime.Now;
                        }
                    }

                    // === OUTFIT BUILDER ITEM SELECTION CYCLING (ID 2) ===
                    if (tobj.SymbolID == 2 && outfitbuilder)
                    {
                        if ((DateTime.Now - lastSelectionTime).TotalMilliseconds > 400)
                        {
                            // Build list of items currently in cart
                            List<string> cartItems = new List<string>();
                            foreach (var entry in cart)
                            {
                                if (entry.Value > 0)
                                    cartItems.Add(entry.Key);
                            }

                            if (cartItems.Count > 0)
                            {
                                int currentIdx = cartItems.IndexOf(hoodieColor);
                                if (currentIdx < 0) currentIdx = 0;

                                // Rotate clockwise - move to next item
                                if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 120)
                                {
                                    currentIdx = (currentIdx + 1) % cartItems.Count;
                                }
                                // Rotate counter-clockwise - move to previous item
                                else if (tobj.AngleDegrees > 240 && tobj.AngleDegrees < 340)
                                {
                                    currentIdx = (currentIdx - 1 + cartItems.Count) % cartItems.Count;
                                }

                                hoodieColor = cartItems[currentIdx];

                                // Update scroll index to ensure selected item is visible
                                if (currentIdx < cartScrollIndex)
                                    cartScrollIndex = currentIdx;
                                else if (currentIdx >= cartScrollIndex + 4)
                                    cartScrollIndex = currentIdx - 3;

                                Console.WriteLine($"Selected cart item: {hoodieColor} (index {currentIdx})");
                                lastSelectionTime = DateTime.Now;
                            }
                        }
                    }

                    /// Handles the logic for confirming the purchase and displaying the thank you screen when the object with SymbolID 6 is rotated on the checkout page, allowing users to complete their transaction and receive confirmation of their order.
                    if (tobj.SymbolID == 6 && checkout)
                    {

                        checkout = false;
                        clothes = false;
                        home = false;
                        login = false;
                        thankyou = true;

                    }


                    //Handles the logic for switching between the Home cards on the home page using the object with SymbolID 7, allowing users to browse through different featured items or categories by rotating the object while on the home page.
                    if (tobj.SymbolID == 7 && home)
                    {
                        if ((DateTime.Now - homeSwitchTime).TotalMilliseconds > 500)
                        {
                            if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 90)
                            {
                                selectedHomeCard = (selectedHomeCard + 1) % 3;
                                homeSwitchTime = DateTime.Now;
                            }
                            else if (tobj.AngleDegrees > 270 && tobj.AngleDegrees < 340)
                            {
                                selectedHomeCard = (selectedHomeCard - 1 + 3) % 3;
                                homeSwitchTime = DateTime.Now;
                            }
                        }
                    }

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
                                else if (checkout == true)
                                {
                                    checkout = false;
                                    login = true;
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

                    // Handles the logic for scrolling through the bestsellers on the home page using the object with SymbolID 9, allowing users to browse through featured products by rotating the object while on the home page.
                    if (tobj.SymbolID == 9 && (bestsellers || deals))
                    {
                        if ((DateTime.Now - lastScrollTime).TotalMilliseconds > 450)
                        {
                            int oldIndex = scrollIndex;
                            string[] currentList = deals ?
                                new string[] { "Navy Shirt", "Black Hoodie", "Denim Pants", "Black Jacket", "Pink Hoodie", "Burgundy Shirt", "Black Pants", "Denim Jacket" } :
                                new string[] { "Navy Shirt", "Black Hoodie", "Denim Pants", "Black Jacket", "Pink Hoodie", "Burgundy Shirt", "Black Pants", "Denim Jacket" };

                            // Scroll Right
                            if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 120)
                            {
                                scrollIndex = (scrollIndex + 1) % currentList.Length;
                            }
                            // Scroll Left
                            else if (tobj.AngleDegrees > 240 && tobj.AngleDegrees < 340)
                            {
                                scrollIndex = (scrollIndex - 1 + currentList.Length) % currentList.Length;
                            }

                            if (oldIndex != scrollIndex)
                            {
                                // FIX: This forces the selection border to move to the new first card
                                hoodieColor = currentList[scrollIndex];
                                lastScrollTime = DateTime.Now;
                            }
                        }
                    }

                    // PLUS BUTTON
                    if (tobj.SymbolID == 3 && (clothes || bestsellers || deals))
                    {
                        if ((DateTime.Now - hoodieCount).TotalMilliseconds > 400)
                        {
                            // Original Clothes Logic
                            if (hoodieColor == "Black") cthoodieBlack++;
                            else if (hoodieColor == "Grey") cthoodieGrey++;
                            else if (hoodieColor == "Burgundy") cthoodieBurgundy++;
                            else if (hoodieColor == "Pink") cthoodiePink++;

                            // Bestsellers / Deals Logic (Full Names)
                            else if (hoodieColor == "Navy Shirt") ctNavyShirt++;
                            else if (hoodieColor == "Black Hoodie") cthoodieBlack++;
                            else if (hoodieColor == "Denim Pants") ctDenimPants++;
                            else if (hoodieColor == "Black Jacket") ctBlackJacket++;
                            else if (hoodieColor == "Pink Hoodie") cthoodiePink++;
                            else if (hoodieColor == "Burgundy Shirt") cthoodieBurgundy++;
                            else if (hoodieColor == "Black Pants") ctBlackPants++;
                            else if (hoodieColor == "Denim Jacket") ctDenimJacket++;

                            hoodieCount = DateTime.Now;
                        }
                    }

                    // MINUS BUTTON
                    if (tobj.SymbolID == 4 && (clothes || bestsellers))
                    {
                        if ((DateTime.Now - hoodieCount).TotalMilliseconds > 400)
                        {
                            if (hoodieColor == "Black Hoodie" && cthoodieBlack > 0) cthoodieBlack--;
                            else if (hoodieColor == "Burgundy Shirt" && cthoodieBurgundy > 0) cthoodieBurgundy--;
                            else if (hoodieColor == "Pink Hoodie" && cthoodiePink > 0) cthoodiePink--;
                            else if (hoodieColor == "Navy Shirt" && ctNavyShirt > 0) ctNavyShirt--;
                            else if (hoodieColor == "Denim Pants" && ctDenimPants > 0) ctDenimPants--;
                            else if (hoodieColor == "Black Jacket" && ctBlackJacket > 0) ctBlackJacket--;
                            else if (hoodieColor == "Black Pants" && ctBlackPants > 0) ctBlackPants--;
                            else if (hoodieColor == "Denim Jacket" && ctDenimJacket > 0) ctDenimJacket--;

                            hoodieCount = DateTime.Now;
                        }
                    }

                    // BACK BUTTON LOGIC
                    if (tobj.SymbolID == 10)
                    {
                        // Check if we are currently in any sub-page
                        if (bestsellers || deals || outfitbuilder || clothes)
                        {
                            // 1. Reset all page booleans
                            bestsellers = false;
                            deals = false;
                            outfitbuilder = false;
                            clothes = false;

                            // 2. Return to Home
                            home = true;

                            // 3. Reset scroll index so the next time we enter a page, it starts at the beginning
                            scrollIndex = 0;

                            Console.WriteLine("Returning to Home Page...");
                        }
                    }

                    // 1. SCROLLING LOGIC (IDs 11 to 15)

                    for (int i = 0; i < scrollerIDs.Length; i++)
                    {
                        if (tobj.SymbolID == scrollerIDs[i])
                        {
                            // Use a cooldown to prevent scrolling too fast
                            if ((DateTime.Now - lastScrollTime).TotalMilliseconds > 400)
                            {
                                // Rotate Right
                                if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 120)
                                    scrollIndices[i]++;
                                // Rotate Left
                                else if (tobj.AngleDegrees > 240 && tobj.AngleDegrees < 340)
                                    scrollIndices[i] = (scrollIndices[i] > 0) ? scrollIndices[i] - 1 : 100; // Large number to prevent negative

                                lastScrollTime = DateTime.Now;
                            }
                        }
                    }

                    // Inside TUIO Update Loop
                    if (objectList.ContainsKey(2)) // Selection Mode Active
                    {
                        // PLUS (ID 3)
                        if (tobj.SymbolID == 3 && (DateTime.Now - hoodieCount).TotalMilliseconds > 500)
                        {
                            if (!string.IsNullOrEmpty(hoodieColor))
                            {
                                if (!cart.ContainsKey(hoodieColor)) cart[hoodieColor] = 0;
                                cart[hoodieColor]++;
                                hoodieCount = DateTime.Now;
                            }
                        }

                        // MINUS (ID 4)
                        if (tobj.SymbolID == 4 && (DateTime.Now - hoodieCount).TotalMilliseconds > 500)
                        {
                            if (cart.ContainsKey(hoodieColor) && cart[hoodieColor] > 0)
                            {
                                cart[hoodieColor]--;
                                hoodieCount = DateTime.Now;
                            }
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

    // Add this helper method to the class (outside OnPaintBackground)
    private bool HasObjectWithSymbolID(int symbolID)
    {
        foreach (TuioObject obj in objectList.Values)
        {
            if (obj.SymbolID == symbolID)
                return true;
        }
        return false;
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