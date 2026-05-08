using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
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
    public bool home = true, login = false, clothes = false, checkout = false, dark = false, thankyou = false, bestsellers = false, deals = false, outfitbuilder = false, loginsteps = false, signupsteps = false;
    int ctBlackJacket = 0, ctDenimJacket = 0, ctDenimPants = 0, ctNavyShirt = 0, ctBlackPants = 0, ctBurgundyShirt = 0, ctBlackHoodie = 0, ctPinkHoodie = 0;

    /// Represents the root file system path for assets.
    private readonly string assetRootPath;
    private readonly string bluetoothStatePath;

    private string bluetoothSigninStatus = "searching";
    private string bluetoothUsername = "";
    private string bluetoothMac = "";
    private string loginStatusMessage = "Searching for recently connected Bluetooth device...";
    private DateTime lastBluetoothPollTime = DateTime.MinValue;
    private DateTime bluetoothSignedInAt = DateTime.MinValue;
    private string bluetoothIdentity = "";
    private readonly TimeSpan bluetoothPollInterval = TimeSpan.FromSeconds(1);
    private readonly TimeSpan loginAutoReturnDelay = TimeSpan.FromSeconds(2);
    private AdaptiveUIController _adaptive;

    /// Represents the current theme path, which can be switched between Light and Dark themes based on user interactions.
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
    int selectionIndex = 0; // Which visible card is selected (0-3)
    DateTime lastScrollTime = DateTime.Now;
    DateTime lastSelectionTime = DateTime.Now;

    // --- Outfit Builder Globals ---
    string currentTop;    // Default starting top
    string currentBottom; // Default starting bottom
    int cartScrollIndex = 0; // For scrolling through cart items in outfit builder
    int selectedMenuCategory = -1; // -1 = no category selected, 0-4 = category index
    int menuHoverIndex = 0; // Current hover position in the circular menu (0-4)
    DateTime lastMenuRotateTime = DateTime.Now;

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

    public DateTime hoodieCount = DateTime.MinValue;

    public int cooldownSeconds = 1;
    public int pageCooldown = 1;
    public int hoodieCooldown = 1;

    int selectedHomeCard = 0;
    int selectedLoginCard = 0;
    DateTime homeSwitchTime = DateTime.Now;

    private string hoodieColor = "Black";

    private int cthoodieBlack = 0;
    private int cthoodieGrey = 0;
    private int cthoodieBurgundy = 0;
    private int cthoodiePink = 0;

    DateTime lastOutfitSelectTime = DateTime.MinValue;

    private System.Windows.Forms.Timer themeTimer = new System.Windows.Forms.Timer();
    private float _baseFontSize = 12f;

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
        _baseFontSize = this.Font.Size;

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
        bluetoothStatePath = ResolveBluetoothStatePath();
        themePath = Path.Combine(assetRootPath, "Light");

        _adaptive = new AdaptiveUIController(this);
        _adaptive.StateChanged += OnAdaptiveStateChanged;
        _adaptive.Start();

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
        _adaptive?.Dispose();
        System.Environment.Exit(0);
    }

    private void OnAdaptiveStateChanged(object sender, AdaptiveStateChangedEventArgs e)
    {
        switch (e.State)
        {
            case AdaptiveState.Confused:
                ShowHelpOverlay(true);
                SetFontScale(1.25f);
                break;

            case AdaptiveState.Frustrated:
                ShowHelpOverlay(true);
                SetFontScale(1.25f);
                break;

            case AdaptiveState.SustainedFrustration:
                ShowHelpOverlay(true);
                ShowAttendantButton(true);
                NavigateToHome();
                break;

            case AdaptiveState.Engaged:
            case AdaptiveState.Interested:
                ShowHelpOverlay(false);
                ShowUpsellPanel(true);
                SetFontScale(1.0f);
                break;

            case AdaptiveState.Disengaged:
                ShowUpsellPanel(false);
                PlayAttractAnimation();
                break;

            case AdaptiveState.Neutral:
            default:
                ShowHelpOverlay(false);
                ShowUpsellPanel(false);
                ShowAttendantButton(false);
                SetFontScale(1.0f);
                break;
        }

        System.Diagnostics.Debug.WriteLine($"[AdaptiveUI] {e.Emotion} -> {e.State} (conf={e.Confidence:P0})");
    }

    private void SetFontScale(float scale)
    {
        this.Font = new Font(this.Font.FontFamily, _baseFontSize * scale);
        Invalidate();
    }

    private void ShowHelpOverlay(bool visible)
    {
        // helpOverlayPanel.Visible = visible;
    }

    private void ShowUpsellPanel(bool visible)
    {
        // upsellPanel.Visible = visible;
    }

    private void ShowAttendantButton(bool visible)
    {
        // attendantButton.Visible = visible;
    }

    private void NavigateToHome()
    {
        home = true;
        login = false;
        clothes = false;
        checkout = false;
        thankyou = false;
        bestsellers = false;
        deals = false;
        outfitbuilder = false;
        loginsteps = false;
        signupsteps = false;
        Invalidate();
    }

    private void PlayAttractAnimation()
    {
        Invalidate();
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

        UpdateBluetoothSigninState();

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

        void DrawNavigationBar(string currentPage)
        {
            float cw = ClientSize.Width;
            float navY = 70; // Height from the top

            // Define the Nav Items
            string[] navItems = { "Home", "Login/Signup", "Cart" };

            // Styling
            Font navFont = new Font("Segoe UI", 20f, FontStyle.Regular);
            Brush navBrush = dark ? Brushes.White : Brushes.Black;
            Pen underlinePen = new Pen(dark ? Color.White : Color.FromArgb(222, 200, 150), 3);

            // Layout math: Distribute across the top-right or center
            float startX = cw * 0.22f; // Adjust this to move the whole bar left/right
            float itemSpacing = cw * 0.23f;

            for (int i = 0; i < navItems.Length; i++)
            {
                string item = navItems[i];
                SizeF textSize = g.MeasureString(item, navFont);
                float itemX = startX + (i * itemSpacing);

                // Draw the Text
                if (item != "Cart")
                {
                    g.DrawString(item, navFont, navBrush, itemX, navY);
                }
                else
                {
                    g.DrawString(item, navFont, navBrush, itemX + 100, navY);
                }

                // Draw the Underline if it's the current page
                if (item.Equals(currentPage, StringComparison.OrdinalIgnoreCase) ||
                   (item == "Login/Signup" && currentPage == "Login"))
                {
                    float lineY = navY + textSize.Height + 5;
                    // Draw the underline bar slightly wider than the text
                    if (item == "Cart")
                    {
                        g.DrawLine(underlinePen, itemX + 100, lineY, itemX + 100 + textSize.Width, lineY);
                    }
                    else
                    {
                        g.DrawLine(underlinePen, itemX, lineY, itemX + textSize.Width, lineY);
                    }
                }
            }
        }

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

                // --- ADDED NAVIGATION BAR HERE ---
                DrawNavigationBar("Home");

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;

                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                        g.DrawImage(logo, -50, -40, 350, 300);
                }
                catch { }

                // 2. Card Layout
                float cardWidth = cw * 0.22f;
                float cardHeight = ch * 0.35f;
                float spacing = cw * 0.06f;
                float startX = (cw - (3 * cardWidth + 2 * spacing)) / 2;
                float baseY = ch * 0.35f;

                // 3. Colors
                Color cardColor;
                Color shadowColor;
                Color selectionColor;
                Brush textBrush;
                if (dark)
                {
                    cardColor = Color.FromArgb(130, 130, 130);
                    shadowColor = Color.FromArgb(70, 70, 70);
                    selectionColor = Color.White;
                    textBrush = Brushes.White;
                }
                else
                {
                    cardColor = Color.FromArgb(222, 200, 150);
                    shadowColor = Color.FromArgb(180, 160, 110);
                    selectionColor = Color.FromArgb(100, 70, 40);
                    textBrush = Brushes.Black;
                }
                Font textFont = new Font("Vladimir Script", 36f, FontStyle.Regular);

                string[] titles = { "Bestsellers", "Deals", "Outfit Builder" };
                string[] images = { "Bestsellers.png", "Deals.png", "OutfitBuilder.png" };

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 4. Draw the Cards
                for (int i = 0; i < 3; i++)
                {
                    bool isSelected = (selectedHomeCard == i);

                    float x = startX + i * (cardWidth + spacing);
                    float y;
                    float shadowOffset;
                    if (isSelected)
                    {
                        y = baseY - 15;
                        shadowOffset = 15;
                    }
                    else
                    {
                        y = baseY;
                        shadowOffset = 10;
                    }

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
                        Color penColor;
                        int penWidth;
                        if (isSelected)
                        {
                            penColor = selectionColor;
                            penWidth = 3;
                        }
                        else
                        {
                            penColor = Color.White;
                            penWidth = 1;
                        }
                        using (Pen highlightPen = new Pen(penColor, penWidth))
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

                // --- ADDED NAVIGATION BAR HERE ---
                DrawNavigationBar("Login");

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;

                // Draw Logo
                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                    {
                        g.DrawImage(logo, -50, -40, 350, 300);
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

                Color selectionColor;
                Brush textBrush;
                if (dark)
                {
                    selectionColor = Color.White;
                    textBrush = Brushes.White;
                }
                else
                {
                    selectionColor = Color.FromArgb(100, 70, 40);
                    textBrush = Brushes.Black;
                }
                Brush circleBrush = new SolidBrush(circleColor);
                Brush shadowBrush = new SolidBrush(shadowColor);
                Font loginFont = new Font("Vladimir Script", 66f, FontStyle.Regular);

                // 4. Position Circles
                string[] labels = { "Login", "Signup" };
                float circleSize = cw * 0.25f; // Diameter of the circles
                float spacing = cw * 0.15f;    // Space between them
                float totalWidth = (2 * circleSize) + spacing;
                float startX = (cw - totalWidth) / 2;
                float centerY = (ch - circleSize) / 2;

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 5. Drawing Loop
                for (int i = 0; i < 2; i++)
                {
                    bool isSelected = (selectedLoginCard == i);

                    float x = startX + i * (circleSize + spacing);
                    float y;
                    float shadowOffset;
                    if (isSelected)
                    {
                        y = centerY - 15;
                        shadowOffset = 15;
                    }
                    else
                    {
                        y = centerY;
                        shadowOffset = 12;
                    }

                    RectangleF circleRect = new RectangleF(x, y, circleSize, circleSize);

                    // Shadow
                    g.FillEllipse(shadowBrush, x + shadowOffset, y + shadowOffset, circleSize, circleSize);

                    // Selection Border (like home page)
                    if (isSelected)
                    {
                        using (Pen selPen = new Pen(selectionColor, 5))
                        {
                            g.DrawEllipse(selPen, x - 5, y - 5, circleSize + 10, circleSize + 10);
                        }
                    }

                    // Main Circle
                    g.FillEllipse(circleBrush, circleRect);

                    // Border
                    Color borderColor;
                    int borderWidth;
                    if (isSelected)
                    {
                        borderColor = selectionColor;
                        borderWidth = 3;
                    }
                    else
                    {
                        borderColor = Color.White;
                        borderWidth = 2;
                    }
                    using (Pen borderPen = new Pen(borderColor, borderWidth))
                    {
                        g.DrawEllipse(borderPen, circleRect);
                    }

                    // Draw Text
                    SizeF textSize = g.MeasureString(labels[i], loginFont);
                    float tx = x + (circleSize - textSize.Width) / 2;
                    float ty = y + (circleSize - textSize.Height) / 2;
                    g.DrawString(labels[i], loginFont, textBrush, tx, ty);
                }

                using (Font statusFont = new Font("Segoe UI", 22f, FontStyle.Bold))
                using (Font hintFont = new Font("Segoe UI", 14f, FontStyle.Regular))
                {
                    string headline;
                    string subline;

                    if (bluetoothSigninStatus == "signed_in" && !string.IsNullOrWhiteSpace(bluetoothUsername))
                    {
                        headline = "Detected user: " + bluetoothUsername;
                        subline = "Returning to Home in 2 seconds...";
                    }
                    else
                    {
                        headline = loginStatusMessage;
                        if (bluetoothSigninStatus == "login_required")
                            subline = "Your phone or headset must stay connected (see devices_db.json).";
                        else if (bluetoothSigninStatus == "searching")
                            subline = "Enable Bluetooth in Windows settings, then try again.";
                        else
                            subline = "Keep your paired Bluetooth device connected.";
                    }

                    SizeF headlineSize = g.MeasureString(headline, statusFont);
                    float headlineX = (cw - headlineSize.Width) / 2;
                    float headlineY = ch * 0.85f;
                    g.DrawString(headline, statusFont, textBrush, headlineX, headlineY);

                    SizeF sublineSize = g.MeasureString(subline, hintFont);
                    float sublineX = (cw - sublineSize.Width) / 2;
                    float sublineY = headlineY + headlineSize.Height + 8;
                    g.DrawString(subline, hintFont, textBrush, sublineX, sublineY);
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


        /// Draws The Login Steps Screen
        void DrawLoginStepsScreen()
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
                    {
                        g.DrawImage(logo, -50, -40, 350, 300);
                    }
                }
                catch { }

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Setup Colors - Same as other cards in the app
                Color cardColor;
                Color shadowColor;
                Brush textBrush;
                if (dark)
                {
                    cardColor = Color.FromArgb(130, 130, 130);
                    shadowColor = Color.FromArgb(70, 70, 70);
                    textBrush = Brushes.White;
                }
                else
                {
                    cardColor = Color.FromArgb(235, 215, 160);
                    shadowColor = Color.FromArgb(180, 160, 110);
                    textBrush = Brushes.Black;
                }

                // Card dimensions - same style as home page
                float cardWidth = cw * 0.28f;
                float cardHeight = 90f;
                float cardX = (cw - cardWidth) / 2;

                // Card 1: Open Your Bluetooth
                float card1Y = ch * 0.35f;
                RectangleF card1Rect = new RectangleF(cardX, card1Y, cardWidth, cardHeight);
                RectangleF shadow1Rect = new RectangleF(cardX + 10, card1Y + 10, cardWidth, cardHeight);

                using (GraphicsPath shadow1Path = RoundedRect(shadow1Rect, 30))
                    g.FillPath(new SolidBrush(shadowColor), shadow1Path);
                using (GraphicsPath card1Path = RoundedRect(card1Rect, 30))
                {
                    g.FillPath(new SolidBrush(cardColor), card1Path);
                    g.DrawPath(new Pen(Color.White, 2), card1Path);
                }
                using (Font cardFont = new Font("Segoe UI", 20, FontStyle.Regular))
                {
                    string txt1 = "Open Your Bluetooth";
                    SizeF sz1 = g.MeasureString(txt1, cardFont);
                    g.DrawString(txt1, cardFont, textBrush, cardX + (cardWidth - sz1.Width) / 2, card1Y + (cardHeight - sz1.Height) / 2);
                }

                // Card 2: Connect to DELLG15
                float card2Y = ch * 0.55f;
                RectangleF card2Rect = new RectangleF(cardX, card2Y, cardWidth, cardHeight);
                RectangleF shadow2Rect = new RectangleF(cardX + 10, card2Y + 10, cardWidth, cardHeight);

                using (GraphicsPath shadow2Path = RoundedRect(shadow2Rect, 30))
                    g.FillPath(new SolidBrush(shadowColor), shadow2Path);
                using (GraphicsPath card2Path = RoundedRect(card2Rect, 30))
                {
                    g.FillPath(new SolidBrush(cardColor), card2Path);
                    g.DrawPath(new Pen(Color.White, 2), card2Path);
                }
                using (Font cardFont = new Font("Segoe UI", 20, FontStyle.Regular))
                {
                    string txt2 = "Connect to DELLG15";
                    SizeF sz2 = g.MeasureString(txt2, cardFont);
                    g.DrawString(txt2, cardFont, textBrush, cardX + (cardWidth - sz2.Width) / 2, card2Y + (cardHeight - sz2.Height) / 2);
                }

                // === BACK BUTTON (ID 10) ===
                float backBtnSize = 70f;
                float margin = 30f;
                RectangleF backRect = new RectangleF(cw - backBtnSize - margin, margin, backBtnSize, backBtnSize);
                bool backActive = false;
                lock (objectList)
                {
                    foreach (TuioObject obj in objectList.Values)
                    {
                        if (obj.SymbolID == 10)
                        {
                            backActive = true;
                            break;
                        }
                    }
                }

                Color btnBaseColor;
                if (dark)
                {
                    btnBaseColor = Color.FromArgb(70, 90, 120);
                }
                else
                {
                    btnBaseColor = Color.FromArgb(255, 190, 100);
                }

                Color backBtnColor;
                Color backPenColor;
                if (dark)
                {
                    backPenColor = Color.White;
                }
                else
                {
                    backPenColor = Color.Black;
                }
                if (backActive)
                {
                    backBtnColor = Color.Gold;
                }
                else
                {
                    backBtnColor = btnBaseColor;
                }

                g.FillEllipse(new SolidBrush(shadowColor), backRect.X, backRect.Y + 4, backBtnSize, backBtnSize);
                g.FillEllipse(new SolidBrush(backBtnColor), backRect);
                g.DrawEllipse(new Pen(backPenColor, 2), backRect);

                using (Font backFont = new Font("Segoe UI", 12, FontStyle.Bold))
                {
                    string backTxt = "BACK";
                    SizeF backSz = g.MeasureString(backTxt, backFont);
                    g.DrawString(backTxt, backFont, textBrush, backRect.X + (backBtnSize - backSz.Width) / 2 + 2, backRect.Y + (backBtnSize - backSz.Height) / 2);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error drawing Login Steps Screen: " + ex.Message);
            }
        }
        if (loginsteps == true)
        {
            DrawLoginStepsScreen();
        }
        ///


        /// Draws The Signup Steps Screen
        void DrawSignupStepsScreen()
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
                    {
                        g.DrawImage(logo, -50, -40, 350, 300);
                    }
                }
                catch { }

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Setup Colors - Same as other cards in the app
                Color cardColor;
                Color shadowColor;
                Brush textBrush;
                if (dark)
                {
                    cardColor = Color.FromArgb(130, 130, 130);
                    shadowColor = Color.FromArgb(70, 70, 70);
                    textBrush = Brushes.White;
                }
                else
                {
                    cardColor = Color.FromArgb(235, 215, 160);
                    shadowColor = Color.FromArgb(180, 160, 110);
                    textBrush = Brushes.Black;
                }

                // Card dimensions - same style as home page
                float cardWidth = cw * 0.28f;
                float cardHeight = 90f;
                float cardX = (cw - cardWidth) / 2;

                // Card 1: Open Your Bluetooth
                float card1Y = ch * 0.28f;
                RectangleF card1Rect = new RectangleF(cardX, card1Y, cardWidth, cardHeight);
                RectangleF shadow1Rect = new RectangleF(cardX + 10, card1Y + 10, cardWidth, cardHeight);

                using (GraphicsPath shadow1Path = RoundedRect(shadow1Rect, 30))
                    g.FillPath(new SolidBrush(shadowColor), shadow1Path);
                using (GraphicsPath card1Path = RoundedRect(card1Rect, 30))
                {
                    g.FillPath(new SolidBrush(cardColor), card1Path);
                    g.DrawPath(new Pen(Color.White, 2), card1Path);
                }
                using (Font cardFont = new Font("Segoe UI", 20, FontStyle.Regular))
                {
                    string txt1 = "Open Your Bluetooth";
                    SizeF sz1 = g.MeasureString(txt1, cardFont);
                    g.DrawString(txt1, cardFont, textBrush, cardX + (cardWidth - sz1.Width) / 2, card1Y + (cardHeight - sz1.Height) / 2);
                }

                // Card 2: Pair to DELLG15
                float card2Y = ch * 0.45f;
                RectangleF card2Rect = new RectangleF(cardX, card2Y, cardWidth, cardHeight);
                RectangleF shadow2Rect = new RectangleF(cardX + 10, card2Y + 10, cardWidth, cardHeight);

                using (GraphicsPath shadow2Path = RoundedRect(shadow2Rect, 30))
                    g.FillPath(new SolidBrush(shadowColor), shadow2Path);
                using (GraphicsPath card2Path = RoundedRect(card2Rect, 30))
                {
                    g.FillPath(new SolidBrush(cardColor), card2Path);
                    g.DrawPath(new Pen(Color.White, 2), card2Path);
                }
                using (Font cardFont = new Font("Segoe UI", 20, FontStyle.Regular))
                {
                    string txt2 = "Pair to DELLG15";
                    SizeF sz2 = g.MeasureString(txt2, cardFont);
                    g.DrawString(txt2, cardFont, textBrush, cardX + (cardWidth - sz2.Width) / 2, card2Y + (cardHeight - sz2.Height) / 2);
                }

                // Card 3: Accept Connection Request
                float card3Y = ch * 0.62f;
                float card3Height = cardHeight + 20;
                RectangleF card3Rect = new RectangleF(cardX, card3Y, cardWidth, card3Height);
                RectangleF shadow3Rect = new RectangleF(cardX + 10, card3Y + 10, cardWidth, card3Height);

                using (GraphicsPath shadow3Path = RoundedRect(shadow3Rect, 30))
                    g.FillPath(new SolidBrush(shadowColor), shadow3Path);
                using (GraphicsPath card3Path = RoundedRect(card3Rect, 30))
                {
                    g.FillPath(new SolidBrush(cardColor), card3Path);
                    g.DrawPath(new Pen(Color.White, 2), card3Path);
                }
                using (Font cardFont = new Font("Segoe UI", 20, FontStyle.Regular))
                {
                    string txt3Line1 = "Accept Connection";
                    string txt3Line2 = "Request";
                    SizeF sz3a = g.MeasureString(txt3Line1, cardFont);
                    SizeF sz3b = g.MeasureString(txt3Line2, cardFont);
                    float lineHeight = sz3a.Height;
                    float totalHeight = lineHeight * 2;
                    float startY = card3Y + (card3Height - totalHeight) / 2;
                    g.DrawString(txt3Line1, cardFont, textBrush, cardX + (cardWidth - sz3a.Width) / 2, startY);
                    g.DrawString(txt3Line2, cardFont, textBrush, cardX + (cardWidth - sz3b.Width) / 2, startY + lineHeight);
                }

                // === BACK BUTTON (ID 10) ===
                float backBtnSize = 70f;
                float margin = 30f;
                RectangleF backRect = new RectangleF(cw - backBtnSize - margin, margin, backBtnSize, backBtnSize);
                bool backActive = false;
                lock (objectList)
                {
                    foreach (TuioObject obj in objectList.Values)
                    {
                        if (obj.SymbolID == 10)
                        {
                            backActive = true;
                            break;
                        }
                    }
                }

                Color btnBaseColor;
                if (dark)
                {
                    btnBaseColor = Color.FromArgb(70, 90, 120);
                }
                else
                {
                    btnBaseColor = Color.FromArgb(255, 190, 100);
                }

                Color backBtnColor;
                Color backPenColor;
                if (dark)
                {
                    backPenColor = Color.White;
                }
                else
                {
                    backPenColor = Color.Black;
                }
                if (backActive)
                {
                    backBtnColor = Color.Gold;
                }
                else
                {
                    backBtnColor = btnBaseColor;
                }

                g.FillEllipse(new SolidBrush(shadowColor), backRect.X, backRect.Y + 4, backBtnSize, backBtnSize);
                g.FillEllipse(new SolidBrush(backBtnColor), backRect);
                g.DrawEllipse(new Pen(backPenColor, 2), backRect);

                using (Font backFont = new Font("Segoe UI", 12, FontStyle.Bold))
                {
                    string backTxt = "BACK";
                    SizeF backSz = g.MeasureString(backTxt, backFont);
                    g.DrawString(backTxt, backFont, textBrush, backRect.X + (backBtnSize - backSz.Width) / 2 + 2, backRect.Y + (backBtnSize - backSz.Height) / 2);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error drawing Signup Steps Screen: " + ex.Message);
            }
        }
        if (signupsteps == true)
        {
            DrawSignupStepsScreen();
        }
        ///


        /// Draws The Clothes Screen
        void DrawClothesScreen()
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
                        g.DrawImage(logo, -50, -40, 350, 300);
                }
                catch { }

                // 2. Layout Setup
                string[] hoodieNames = { "Black", "Grey", "Burgundy", "Pink" };
                string[] hoodieFiles = { "BlackHoodie.png", "GreyHoodie.png", "BurgundyHoodie.png", "PinkHoodie.png" };
                int[] counts = { cthoodieBlack, cthoodieGrey, cthoodieBurgundy, cthoodiePink };

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
                Color selectionColor = dark ? Color.White : Color.FromArgb(100, 70, 40); // Same as Home Screen

                Brush textBrush = dark ? Brushes.White : Brushes.Black;
                Pen btnOutline = new Pen(Color.Black, 2);

                g.SmoothingMode = SmoothingMode.AntiAlias;

                for (int i = 0; i < 4; i++)
                {
                    bool isSelected = (hoodieColor == hoodieNames[i]);

                    float x = startX + i * (cardWidth + spacing);
                    // ANIMATION: Lift the card if selected
                    float y = isSelected ? baseY - 15 : baseY;
                    float shadowOffset = isSelected ? 15 : 10;

                    RectangleF rect = new RectangleF(x, y, cardWidth, cardHeight);
                    RectangleF shadowRect = new RectangleF(x + shadowOffset, y + shadowOffset, cardWidth, cardHeight);

                    // A. Draw Shadow
                    using (GraphicsPath shadowPath = RoundedRect(shadowRect, 25))
                        g.FillPath(new SolidBrush(shadowColor), shadowPath);

                    // B. Draw Secondary Selection Border (Same as Home Screen)
                    if (isSelected)
                    {
                        using (GraphicsPath borderPath = RoundedRect(new RectangleF(x - 5, y - 5, cardWidth + 10, cardHeight + 10), 30))
                        using (Pen selPen = new Pen(selectionColor, 4))
                        {
                            g.DrawPath(selPen, borderPath);
                        }
                    }

                    // C. Draw Main Card
                    using (GraphicsPath path = RoundedRect(rect, 25))
                    {
                        g.FillPath(new SolidBrush(cardColor), path);
                        using (Pen highlightPen = new Pen(isSelected ? selectionColor : Color.White, isSelected ? 3 : 1))
                            g.DrawPath(highlightPen, path);
                    }

                    // D. Draw Hoodie Image
                    try
                    {
                        using (Bitmap imgg = new Bitmap(Path.Combine(themePath, hoodieFiles[i])))
                            g.DrawImage(imgg, x, y - 10, cardWidth, cardHeight + 20);
                    }
                    catch { }

                    // E. Controls Bar (Fixed position relative to card)
                    float controlY = ch * 0.70f;
                    float btnSize = 55f;
                    RectangleF barRect = new RectangleF(x, controlY, cardWidth, btnSize);
                    using (GraphicsPath barPath = RoundedRect(barRect, 30))
                        g.FillPath(new SolidBrush(barColor), barPath);

                    // Plus/Minus Logic
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

                    // Quantity Count
                    using (Font f = new Font("Arial", 28, FontStyle.Bold))
                    {
                        string s = counts[i].ToString();
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
            }
            catch (Exception ex) { Console.WriteLine("Draw Error: " + ex.Message); }
        }
        if (clothes == true)
        {
            DrawClothesScreen();
        }

        /// Draws The Bestsellers Screen
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
                        g.DrawImage(logo, -50, -40, 350, 300);
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
                Color cardColor;
                Color shadowColor;
                Color barColor;
                Color btnBaseColor;
                Color selectionColor;
                Brush textBrush;
                if (dark)
                {
                    cardColor = Color.FromArgb(130, 130, 130);
                    shadowColor = Color.FromArgb(70, 70, 70);
                    barColor = Color.FromArgb(55, 55, 55);
                    btnBaseColor = Color.FromArgb(70, 90, 120);
                    selectionColor = Color.White;
                    textBrush = Brushes.White;
                }
                else
                {
                    cardColor = Color.FromArgb(235, 215, 160);
                    shadowColor = Color.FromArgb(180, 160, 110);
                    barColor = Color.FromArgb(220, 200, 150);
                    btnBaseColor = Color.FromArgb(255, 190, 100);
                    selectionColor = Color.FromArgb(100, 70, 40);
                    textBrush = Brushes.Black;
                }


                Pen btnOutline = new Pen(Color.Black, 2);

                g.SmoothingMode = SmoothingMode.AntiAlias;

                //using (Font arrowFont = new Font("Arial", 22, FontStyle.Bold))
                //{
                //    g.DrawString("◄", arrowFont, new SolidBrush(selectionColor), 35, 380);
                //    //g.DrawString("►", arrowFont, new SolidBrush(selectionColor), 1950, 400);
                //}

                RectangleF rectt;
                // 4. Draw 4 visible cards starting from scrollIndex
                for (int i = 0; i < 4; i++)
                {
                    int itemIdx = (scrollIndex + i) % bestNames.Length;
                    string currentItemName = bestNames[itemIdx];
                    string currentItemFile = bestFiles[itemIdx];

                    // Selection Logic: The white border stays on the first card (scrollIndex)
                    bool isSelected = (hoodieColor == currentItemName);
                    float x = startX + i * (cardWidth + spacing);
                    float y;
                    float shadowOffset;
                    if (isSelected)
                    {
                        y = baseY - 15;
                        shadowOffset = 15;
                    }
                    else
                    {
                        y = baseY;
                        shadowOffset = 10;
                    }

                    RectangleF rect = new RectangleF(x, y, cardWidth, cardHeight);
                    RectangleF shadowRect = new RectangleF(x + shadowOffset, y + shadowOffset, cardWidth, cardHeight);

                    if (i == 0)
                    {
                        using (Font arrowFont = new Font("Arial", 22, FontStyle.Bold))
                        {
                            g.DrawString("◄", arrowFont, new SolidBrush(selectionColor), rect.X - 45, rect.Y + (cardHeight / 2) - 20);
                            //g.DrawString("►", arrowFont, new SolidBrush(selectionColor), rect.Right + 10, rect.Y + (cardHeight / 2) - 20);
                        }
                    }
                    if (i == 3)
                    {
                        using (Font arrowFont = new Font("Arial", 22, FontStyle.Bold))
                        {
                            //g.DrawString("◄", arrowFont, new SolidBrush(selectionColor), rect.X - 45, rect.Y + (cardHeight / 2) - 20);
                            g.DrawString("►", arrowFont, new SolidBrush(selectionColor), rect.Right + 10, rect.Y + (cardHeight / 2) - 20);
                        }
                    }

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
                        Color highlightColor;
                        int highlightWidth;
                        if (isSelected)
                        {
                            highlightColor = selectionColor;
                            highlightWidth = 3;
                        }
                        else
                        {
                            highlightColor = Color.White;
                            highlightWidth = 1;
                        }
                        using (Pen highlightPen = new Pen(highlightColor, highlightWidth))
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
                    Color plusColor;
                    if (plusPressed)
                    {
                        plusColor = Color.Gold;
                    }
                    else
                    {
                        plusColor = btnBaseColor;
                    }
                    using (Brush b = new SolidBrush(plusColor))
                    {
                        g.FillEllipse(b, pRect);
                        g.DrawEllipse(btnOutline, pRect);
                        g.DrawString("+", new Font("Arial", 22, FontStyle.Bold), Brushes.Black, x + 14, controlY + 11);
                    }

                    // Minus Button
                    float mX = x + cardWidth - btnSize;
                    RectangleF mRect = new RectangleF(mX, controlY, btnSize, btnSize);
                    Color minusColor;
                    if (minusPressed)
                    {
                        minusColor = Color.Gold;
                    }
                    else
                    {
                        minusColor = btnBaseColor;
                    }
                    using (Brush b = new SolidBrush(minusColor))
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
                Color backBtnColor;
                if (backActive)
                {
                    backBtnColor = Color.Gold;
                }
                else
                {
                    backBtnColor = btnBaseColor;
                }
                Color backPenColor;
                if (dark)
                {
                    backPenColor = Color.White;
                }
                else
                {
                    backPenColor = Color.Black;
                }
                using (Brush b = new SolidBrush(backBtnColor))
                using (Pen p = new Pen(backPenColor, 2))
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

        /// Draws The Deals Screen
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
                        g.DrawImage(logo, -50, -40, 350, 300);
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
                Color cardColor;
                Color shadowColor;
                Color barColor;
                Color btnBaseColor;
                Color selectionColor;
                Brush textBrush;
                if (dark)
                {
                    cardColor = Color.FromArgb(130, 130, 130);
                    shadowColor = Color.FromArgb(70, 70, 70);
                    barColor = Color.FromArgb(55, 55, 55);
                    btnBaseColor = Color.FromArgb(70, 90, 120);
                    selectionColor = Color.White;
                    textBrush = Brushes.White;
                }
                else
                {
                    cardColor = Color.FromArgb(235, 215, 160);
                    shadowColor = Color.FromArgb(180, 160, 110);
                    barColor = Color.FromArgb(220, 200, 150);
                    btnBaseColor = Color.FromArgb(255, 190, 100);
                    selectionColor = Color.FromArgb(100, 70, 40);
                    textBrush = Brushes.Black;
                }
                Color discountBadgeColor = Color.FromArgb(220, 53, 69);

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
                    float y;
                    float shadowOffset;
                    if (isSelected)
                    {
                        y = baseY - 15;
                        shadowOffset = 15;
                    }
                    else
                    {
                        y = baseY;
                        shadowOffset = 10;
                    }

                    RectangleF rect = new RectangleF(x, y, cardWidth, cardHeight);
                    RectangleF shadowRect = new RectangleF(x + shadowOffset, y + shadowOffset, cardWidth, cardHeight);


                    if (i == 0)
                    {
                        using (Font arrowFont = new Font("Arial", 22, FontStyle.Bold))
                        {
                            g.DrawString("◄", arrowFont, new SolidBrush(selectionColor), rect.X - 45, rect.Y + (cardHeight / 2) - 20);
                            //g.DrawString("►", arrowFont, new SolidBrush(selectionColor), rect.Right + 10, rect.Y + (cardHeight / 2) - 20);
                        }
                    }
                    if (i == 3)
                    {
                        using (Font arrowFont = new Font("Arial", 22, FontStyle.Bold))
                        {
                            //g.DrawString("◄", arrowFont, new SolidBrush(selectionColor), rect.X - 45, rect.Y + (cardHeight / 2) - 20);
                            g.DrawString("►", arrowFont, new SolidBrush(selectionColor), rect.Right + 10, rect.Y + (cardHeight / 2) - 20);
                        }
                    }

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
                        Color penColor;
                        int penWidth;
                        if (isSelected)
                        {
                            penColor = selectionColor;
                            penWidth = 3;
                        }
                        else
                        {
                            penColor = Color.White;
                            penWidth = 1;
                        }
                        using (Pen p = new Pen(penColor, penWidth))
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
                    Color plusColor;
                    if (plusActive)
                    {
                        plusColor = Color.Gold;
                    }
                    else
                    {
                        plusColor = btnBaseColor;
                    }
                    using (Brush b = new SolidBrush(plusColor))
                    {
                        g.FillEllipse(b, pRect);
                        g.DrawEllipse(btnOutline, pRect);
                        g.DrawString("+", new Font("Arial", 22, FontStyle.Bold), Brushes.Black, x + 14, controlY + 11);
                    }

                    // Minus Button UI
                    float mX = x + cardWidth - btnSize;
                    RectangleF mRect = new RectangleF(mX, controlY, btnSize, btnSize);
                    Color minusColor;
                    if (minusActive)
                    {
                        minusColor = Color.Gold;
                    }
                    else
                    {
                        minusColor = btnBaseColor;
                    }
                    using (Brush b = new SolidBrush(minusColor))
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
                Color backBtnColor;
                if (backActive)
                {
                    backBtnColor = Color.Gold;
                }
                else
                {
                    backBtnColor = btnBaseColor;
                }
                Color backPenColor;
                if (dark)
                {
                    backPenColor = Color.White;
                }
                else
                {
                    backPenColor = Color.Black;
                }
                using (Brush b = new SolidBrush(backBtnColor))
                using (Pen p = new Pen(backPenColor, 2))
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


        /// Draws The Outfit Builder Screen
        void DrawOutfitBuilderScreen()
        {
            try
            {
                // 1. Setup Canvas and Theme
                float cw = ClientSize.Width;
                float ch = ClientSize.Height;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Background & Logo
                using (Bitmap bg = new Bitmap(Path.Combine(themePath, "Background.png")))
                {
                    Bitmap tempBg = bg; ResizeImage(ref tempBg); g.DrawImage(tempBg, 0, 0);
                }
                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                        g.DrawImage(logo, -50, -40, 350, 300);
                }
                catch { }

                Color cardCol;
                if (dark)
                {
                    cardCol = Color.FromArgb(100, 100, 100);
                }
                else
                {
                    cardCol = Color.FromArgb(235, 215, 160);
                }
                Color shadowCol;
                if (dark)
                {
                    shadowCol = Color.FromArgb(40, 40, 40);
                }
                else
                {
                    shadowCol = Color.FromArgb(180, 160, 110);
                }
                Color btnCol;
                if (dark)
                {
                    btnCol = Color.FromArgb(70, 90, 120);
                }
                else
                {
                    btnCol = Color.FromArgb(255, 190, 100);
                }
                Color selectionColor;
                if (dark)
                {
                    selectionColor = Color.White;
                }
                else
                {
                    selectionColor = Color.FromArgb(100, 70, 40);
                }
                Color barColor;
                if (dark)
                {
                    barColor = Color.FromArgb(60, 60, 60);
                }
                else
                {
                    barColor = Color.FromArgb(220, 200, 150);
                }
                Color menuSliceColor;
                if (dark)
                {
                    menuSliceColor = Color.FromArgb(80, 80, 80);
                }
                else
                {
                    menuSliceColor = Color.FromArgb(245, 225, 175);
                }
                Color menuSliceSelectedColor;
                if (dark)
                {
                    menuSliceSelectedColor = Color.FromArgb(40, 40, 40);
                }
                else
                {
                    menuSliceSelectedColor = Color.FromArgb(200, 160, 100);
                }
                Color menuSliceHoverColor;
                if (dark)
                {
                    menuSliceHoverColor = Color.FromArgb(150, 150, 150);
                }
                else
                {
                    menuSliceHoverColor = Color.FromArgb(255, 240, 200);
                }

                // FIXED: Use this brush for ALL text to prevent disappearing in Dark Mode
                Brush textBrush;
                if (dark)
                {
                    textBrush = Brushes.White;
                }
                else
                {
                    textBrush = Brushes.Black;
                }
                Color borderColor;
                if (dark)
                {
                    borderColor = Color.Gray;
                }
                else
                {
                    borderColor = Color.White;
                }
                Pen borderPen = new Pen(borderColor, 2);
                Pen selectionPen = new Pen(selectionColor, 3);

                // === LEFT PART: HUGE OUTFIT CARD (PREVIEW) ===
                float leftWidth = cw * 0.65f;
                float previewCardW = leftWidth * 0.75f;
                float previewCardH = ch * 0.55f;
                float previewCardX = (leftWidth - previewCardW) / 2 + 45;
                float previewRectY = 80;
                RectangleF previewRect = new RectangleF(previewCardX, previewRectY, previewCardW, previewCardH);

                g.FillPath(new SolidBrush(shadowCol), RoundedRect(new RectangleF(previewRect.X + 12, previewRect.Y + 12, previewRect.Width, previewRect.Height), 40));
                g.FillPath(new SolidBrush(cardCol), RoundedRect(previewRect, 40));
                g.DrawPath(borderPen, RoundedRect(previewRect, 40));

                // Draw Outfit Layers
                if (!string.IsNullOrEmpty(currentTop))
                {
                    string path = Path.Combine(themePath, currentTop + ".png");
                    if (File.Exists(path)) using (Bitmap img = new Bitmap(path))
                            g.DrawImage(img, previewRect.X + 50, previewRect.Y - 10, previewRect.Width - 100, previewRect.Height * 0.70f);
                }
                if (!string.IsNullOrEmpty(currentBottom))
                {
                    string path = Path.Combine(themePath, currentBottom + ".png");
                    if (File.Exists(path)) using (Bitmap img = new Bitmap(path))
                            g.DrawImage(img, previewRect.X + 80, previewRect.Y - 10 + (previewRect.Height * 0.38f), previewRect.Width - 160, previewRect.Height * 0.70f);
                }

                // === ADD TO CART BUTTON (ID 5) ===
                bool addCartActive = HasObjectWithSymbolID(5);
                // Corrected X offset logic to stay relative to previewRect
                RectangleF addCartRect = new RectangleF(previewRect.X + (previewRect.Width - 200) / 2 + 250, previewRect.Bottom + 20, 200, 55);

                g.FillPath(new SolidBrush(shadowCol), RoundedRect(new RectangleF(addCartRect.X, addCartRect.Y + 5, addCartRect.Width, addCartRect.Height), 20));
                g.FillPath(new SolidBrush(addCartActive ? Color.Gold : btnCol), RoundedRect(addCartRect, 20));
                g.DrawPath(new Pen(dark ? Color.White : Color.Black, 2), RoundedRect(addCartRect, 20));

                using (Font btnFont = new Font("Segoe UI", 13, FontStyle.Bold))
                {
                    string txt = "ADD TO CART";
                    SizeF sz = g.MeasureString(txt, btnFont);
                    // FIXED: Using textBrush instead of Brushes.Black
                    g.DrawString(txt, btnFont, textBrush, addCartRect.X + (addCartRect.Width - sz.Width) / 2, addCartRect.Y + (addCartRect.Height - sz.Height) / 2);
                }

                // === CART PREVIEW (BOTTOM LEFT) ===
                List<string> cartItemsList = new List<string>();
                foreach (var entry in cart)
                {
                    if (entry.Value > 0)
                    {
                        cartItemsList.Add(entry.Key);
                    }
                }

                if (cartItemsList.Count > 0)
                {
                    float cpX = 30, cpY = ch - 320, cpW = leftWidth * 0.65f, cpH = 300;
                    RectangleF cpRect = new RectangleF(cpX, cpY, cpW, cpH);
                    g.FillPath(new SolidBrush(shadowCol), RoundedRect(new RectangleF(cpX + 8, cpY + 8, cpW, cpH), 25));
                    g.FillPath(new SolidBrush(cardCol), RoundedRect(cpRect, 25));
                    g.DrawString(String.Format("Cart Preview ({0})", cartItemsList.Count), new Font("Segoe UI", 16, FontStyle.Bold), textBrush, cpX + 20, cpY + 12);

                    float itemY = cpY + 55;
                    using (Font itemFont = new Font("Segoe UI", 10, FontStyle.Bold))
                    using (Font symbolFont = new Font("Arial", 14, FontStyle.Bold))
                    {
                        for (int i = cartScrollIndex; i < cartItemsList.Count && i < cartScrollIndex + 4; i++)
                        {
                            string name = cartItemsList[i];
                            int qty = cart[name];
                            bool isSelected = (hoodieColor == name);

                            if (isSelected) g.DrawPath(selectionPen, RoundedRect(new RectangleF(cpX + 10, itemY - 5, cpW - 20, 50), 12));

                            // 1. Item Image
                            try
                            {
                                string itemPath = Path.Combine(themePath, name + ".png");
                                if (File.Exists(itemPath)) using (Bitmap img = new Bitmap(itemPath))
                                        g.DrawImage(img, cpX + 20, itemY, 40, 40);
                            }
                            catch { }

                            // 2. Item Name
                            g.DrawString(name, itemFont, textBrush, cpX + 70, itemY + 10);

                            // 3. Quantity Pill
                            float pillX = cpX + cpW - 160;
                            RectangleF pillRect = new RectangleF(pillX, itemY + 5, 90, 30);
                            g.FillPath(new SolidBrush(barColor), RoundedRect(pillRect, 15));
                            g.DrawString("-", symbolFont, textBrush, pillX + 10, itemY + 8);
                            g.DrawString(qty.ToString(), itemFont, textBrush, pillX + 40, itemY + 11);
                            g.DrawString("+", symbolFont, textBrush, pillX + 68, itemY + 8);

                            // 4. Delete Icon
                            try
                            {
                                string delPath = Path.Combine(themePath, "Delete.png");
                                if (File.Exists(delPath)) using (Bitmap delImg = new Bitmap(delPath))
                                        g.DrawImage(delImg, pillX + 100, itemY + 5, 30, 30);
                            }
                            catch { g.DrawString("X", itemFont, Brushes.Red, pillX + 105, itemY + 10); }

                            itemY += 55;
                        }
                    }
                }

                // === RIGHT PART: CATEGORY CARD ===
                string[] catNames = { "Shirts", "Hoodies", "Jackets", "Pants", "Shorts" };
                string[] catIcons = { "Shirt.png", "Hoodie.png", "Jacket.png", "Pants.png", "Shorts.png" };

                if (selectedMenuCategory >= 0 && selectedMenuCategory < 5)
                {
                    int catIdx = selectedMenuCategory;
                    float cardSize = Math.Min(cw - leftWidth - 80, ch * 0.45f);
                    float catCardX = leftWidth + ((cw - leftWidth) / 2) - (cardSize / 2) - 70;
                    RectangleF catRect = new RectangleF(catCardX, 80, cardSize, cardSize);

                    bool isScrolling = HasObjectWithSymbolID(11);
                    int itemIdx = Math.Abs(scrollIndices[catIdx]) % items[catIdx].Length;
                    string displayedItem = items[catIdx][itemIdx];

                    g.FillPath(new SolidBrush(shadowCol), RoundedRect(new RectangleF(catRect.X + 12, catRect.Y + 12, cardSize, cardSize), 25));

                    Color fillColor;
                    if (isScrolling)
                    {
                        if (dark)
                        {
                            fillColor = Color.FromArgb(120, 110, 80);
                        }
                        else
                        {
                            fillColor = Color.FromArgb(255, 245, 200);
                        }
                    }
                    else
                    {
                        fillColor = cardCol;
                    }
                    g.FillPath(new SolidBrush(fillColor), RoundedRect(catRect, 25));

                    if (isScrolling)
                    {
                        g.DrawPath(new Pen(selectionColor, 5), RoundedRect(new RectangleF(catRect.X - 5, catRect.Y - 5, cardSize + 10, cardSize + 10), 30));
                    }

                    using (Font arrowFont = new Font("Arial", 22, FontStyle.Bold))
                    {
                        g.DrawString("◄", arrowFont, new SolidBrush(selectionColor), catRect.X - 45, catRect.Y + (cardSize / 2) - 20);
                        g.DrawString("►", arrowFont, new SolidBrush(selectionColor), catRect.Right + 10, catRect.Y + (cardSize / 2) - 20);
                    }

                    g.DrawString(catNames[catIdx], new Font("Segoe UI", 16, FontStyle.Bold), textBrush, catRect.X + 20, catRect.Y + 20);
                    try
                    {
                        using (Bitmap img = new Bitmap(Path.Combine(themePath, displayedItem + ".png")))
                            g.DrawImage(img, catRect.X + 40, catRect.Y + 60, cardSize - 80, cardSize - 120);
                    }
                    catch { }
                    g.DrawString(displayedItem, new Font("Segoe UI", 12, FontStyle.Bold), textBrush, catRect.X + 20, catRect.Bottom - 40);
                }

                // === CIRCULAR PIE MENU ===
                float menuRadius = 130f, menuCenterX = cw - 330, menuCenterY = ch - 220, innerRadius = 45f;
                float sliceAngle = 360f / 5, startAngle = -90 - sliceAngle / 2;

                for (int i = 0; i < 5; i++)
                {
                    float currAngle = startAngle + i * sliceAngle;
                    bool isHovered = (menuHoverIndex == i);
                    bool isSelected = (selectedMenuCategory == i);

                    Color sliceColor;
                    if (isSelected)
                    {
                        sliceColor = menuSliceSelectedColor;
                    }
                    else if (isHovered)
                    {
                        sliceColor = menuSliceHoverColor;
                    }
                    else
                    {
                        sliceColor = menuSliceColor;
                    }

                    using (GraphicsPath slicePath = new GraphicsPath())
                    {
                        slicePath.AddArc(menuCenterX - menuRadius, menuCenterY - menuRadius, menuRadius * 2, menuRadius * 2, currAngle, sliceAngle);
                        slicePath.AddArc(menuCenterX - innerRadius, menuCenterY - innerRadius, innerRadius * 2, innerRadius * 2, currAngle + sliceAngle, -sliceAngle);
                        slicePath.CloseFigure();
                        g.FillPath(new SolidBrush(sliceColor), slicePath);

                        Color penColor;
                        int penWidth;
                        if (isHovered)
                        {
                            penColor = selectionColor;
                            penWidth = 4;
                        }
                        else
                        {
                            if (dark)
                            {
                                penColor = Color.Gray;
                            }
                            else
                            {
                                penColor = Color.White;
                            }
                            penWidth = 1;
                        }
                        g.DrawPath(new Pen(penColor, penWidth), slicePath);
                    }
                    float iconA = (currAngle + sliceAngle / 2) * (float)Math.PI / 180f;
                    float ix = menuCenterX + (float)Math.Cos(iconA) * (menuRadius + innerRadius) / 2 - 20;
                    float iy = menuCenterY + (float)Math.Sin(iconA) * (menuRadius + innerRadius) / 2 - 20;
                    try
                    {
                        using (Bitmap icon = new Bitmap(Path.Combine(themePath, catIcons[i])))
                        {
                            g.DrawImage(icon, ix, iy, 40, 40);
                        }
                    }
                    catch { }
                }

                g.FillEllipse(new SolidBrush(cardCol), menuCenterX - innerRadius, menuCenterY - innerRadius, innerRadius * 2, innerRadius * 2);
                try
                {
                    using (Bitmap mIcon = new Bitmap(Path.Combine(themePath, "Menu.png")))
                    {
                        g.DrawImage(mIcon, menuCenterX - 25, menuCenterY - 25, 50, 50);
                    }
                }
                catch { }

                // === BACK BUTTON ===
                RectangleF backRect = new RectangleF(cw - 85, 25, 60, 60);
                bool backActive = HasObjectWithSymbolID(10);
                g.FillEllipse(new SolidBrush(shadowCol), backRect.X, backRect.Y + 4, 60, 60);

                Color backFillColor;
                if (backActive)
                {
                    backFillColor = Color.Gold;
                }
                else
                {
                    backFillColor = btnCol;
                }
                g.FillEllipse(new SolidBrush(backFillColor), backRect);

                Color backPenColor;
                if (dark)
                {
                    backPenColor = Color.White;
                }
                else
                {
                    backPenColor = Color.Black;
                }
                g.DrawEllipse(new Pen(backPenColor, 2), backRect);
                g.DrawString("BACK", new Font("Segoe UI", 10, FontStyle.Bold), textBrush, backRect.X + 8, backRect.Y + 20);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Builder Error: " + ex.Message);
            }
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

                // --- ADDED NAVIGATION BAR HERE ---
                DrawNavigationBar("Cart");

                try
                {
                    using (Bitmap logo = new Bitmap(Path.Combine(themePath, "Logo.png")))
                        g.DrawImage(logo, -50, -40, 350, 300);
                }
                catch { }

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;
                int totalItems = 0;
                foreach (KeyValuePair<string, int> item in cart)
                {
                    totalItems = totalItems + item.Value;
                }

                float cardW = cw * 0.60f; // Slightly wider to fit longer names
                float cardH = ch * 0.75f;
                float cardX = (cw - cardW) / 2;
                float cardY = (ch - cardH) / 2 + 40;

                // Theme Colors
                Color cardCol;
                Color shadowCol;
                Color btnCol;
                Brush textBrush;
                Color selectionPenColor;
                if (dark)
                {
                    cardCol = Color.FromArgb(130, 130, 130);
                    shadowCol = Color.FromArgb(70, 70, 70);
                    btnCol = Color.FromArgb(70, 90, 120);
                    textBrush = Brushes.White;
                    selectionPenColor = Color.White;
                }
                else
                {
                    cardCol = Color.FromArgb(235, 215, 160);
                    shadowCol = Color.FromArgb(180, 160, 110);
                    btnCol = Color.FromArgb(255, 190, 100);
                    textBrush = Brushes.Black;
                    selectionPenColor = Color.SaddleBrown;
                }
                Pen selectionPen = new Pen(selectionPenColor, 3);

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
                                string pStr = price.ToString("F2") + "$";
                                g.DrawString(pStr, itemFont, textBrush, cardX + cardW - 50 - g.MeasureString(pStr, itemFont).Width, itemY + 12);

                                itemY += 65; // Spacing for next item
                            }
                        }

                        // Summary Totals
                        itemY = cardY + cardH - 220;
                        double service = subtotal * 0.12;
                        double tax = subtotal * 0.14;
                        double totalVal = subtotal + service + tax;

                        string[,] summary = { { "Subtotal", subtotal.ToString("F2") + "$" }, { "Service (12%)", service.ToString("F2") + "$" }, { "TAX (14%)", tax.ToString("F2") + "$" } };
                        for (int i = 0; i < 3; i++)
                        {
                            g.DrawString(summary[i, 0], itemFont, textBrush, cardX + 50, itemY + (i * 25));
                            g.DrawString(summary[i, 1], itemFont, textBrush, cardX + cardW - 60 - g.MeasureString(summary[i, 1], itemFont).Width, itemY + (i * 25));
                        }

                        // Divider and Grand Total
                        float footerY = cardY + cardH - 95;
                        g.DrawLine(new Pen(textBrush, 2), cardX + 50, footerY - 10, cardX + cardW - 50, footerY - 10);
                        g.DrawString("Total Payment", itemFont, textBrush, cardX + 50, footerY + 15);
                        g.DrawString(totalVal.ToString("F2") + "$", titleFont, textBrush, cardX + 185, footerY + 5);

                        // Checkout Button
                        float btnW = 160; float btnH = 55;
                        float btnX = cardX + cardW - btnW - 40;
                        RectangleF btnRect = new RectangleF(btnX, footerY + 5, btnW, btnH);
                        using (Brush b = new SolidBrush(btnCol))
                            g.FillPath(b, RoundedRect(btnRect, 15));

                        g.DrawPath(new Pen(Color.Black, 2), RoundedRect(btnRect, 15));
                        string checkTxt = "CHECKOUT";
                        SizeF sSize = g.MeasureString(checkTxt, itemFont);
                        Brush checkoutTextBrush;
                        if (dark)
                        {
                            checkoutTextBrush = Brushes.White;
                        }
                        else
                        {
                            checkoutTextBrush = Brushes.Black;
                        }
                        g.DrawString(checkTxt, itemFont, checkoutTextBrush, btnX + (btnW - sSize.Width) / 2, footerY + 5 + (btnH - sSize.Height) / 2);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Checkout Draw Error: " + ex.Message); }
        }
        if (checkout == true)
        {
            DrawCheckoutScreen();
        }

        /// Draws The Thank You Screen
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


        void InitializeThemeTimer()
        {
            themeTimer.Interval = 1; // check every 60 seconds
            themeTimer.Tick += (s, e) => UpdateThemeByTime();
            themeTimer.Start();

        }

        void UpdateThemeByTime()
        {
            int currentHour = DateTime.Now.Hour;
            bool shouldBeDark = !(currentHour >= 6 && currentHour < 15);

            if (dark != shouldBeDark)
            {
                dark = shouldBeDark;
                themePath = Path.Combine(assetRootPath, dark ? "Dark" : "Light");

                Invalidate(); // forces UI redraw immediately
            }
        }

        InitializeThemeTimer();

        ///

        // draw the current cursor point (no path trail)
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

                    // === CHECKOUT DELETE ITEM (ID 12) ===
                    if (tobj.SymbolID == 12 && checkout)
                    {
                        if ((DateTime.Now - hoodieSwitch).TotalMilliseconds > 500)
                        {
                            if (!string.IsNullOrEmpty(hoodieColor) && cart.ContainsKey(hoodieColor) && cart[hoodieColor] > 0)
                            {
                                Console.WriteLine(String.Format("CHECKOUT: Deleting {0} from cart", hoodieColor));
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
                    // === OUTFIT BUILDER SCROLLING (ID 11 for all categories) ===
                    // ========== OUTFIT BUILDER COMPLETE LOGIC ==========
                    if (outfitbuilder)
                    {
                        // --- SCROLLING LOGIC using ID 11 for the selected category ---
                        if (selectedMenuCategory >= 0 && selectedMenuCategory < 5)
                        {
                            int catIdx = selectedMenuCategory;

                            if (tobj.SymbolID == catIdx)
                            {
                                if ((DateTime.Now - lastScrollTime).TotalMilliseconds > 350)
                                {
                                    int oldIdx = scrollIndices[catIdx];

                                    // Rotate Right
                                    if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 120)
                                    {
                                        scrollIndices[catIdx] = (scrollIndices[catIdx] + 1) % items[catIdx].Length;
                                    }
                                    // Rotate Left
                                    else if (tobj.AngleDegrees > 240 && tobj.AngleDegrees < 340)
                                    {
                                        scrollIndices[catIdx] = (scrollIndices[catIdx] - 1 + items[catIdx].Length) % items[catIdx].Length;
                                    }

                                    if (oldIdx != scrollIndices[catIdx])
                                    {
                                        hoodieColor = items[catIdx][scrollIndices[catIdx]];
                                        lastScrollTime = DateTime.Now;
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
                            selectionIndex = 0;

                            if (selectedHomeCard == 0) { bestsellers = true; hoodieColor = bestNames[0]; }
                            else if (selectedHomeCard == 1) { deals = true; hoodieColor = dealNames[0]; }
                            else if (selectedHomeCard == 2)
                            {
                                outfitbuilder = true;
                                // Reset menu selection when entering
                                selectedMenuCategory = -1;
                                menuHoverIndex = 0;
                                hoodieColor = currentTop;
                            }
                        }
                        // Navigation from Login page to LoginSteps or SignupSteps
                        else if (login)
                        {
                            login = false;
                            if (selectedLoginCard == 0)
                            {
                                loginsteps = true;
                                Console.WriteLine("Navigating to Login Steps...");
                            }
                            else if (selectedLoginCard == 1)
                            {
                                signupsteps = true;
                                Console.WriteLine("Navigating to Signup Steps...");
                            }
                        }
                        // Selection within Outfit Builder - Select category from circular menu OR select item from category
                        //else if (outfitbuilder)
                        //{
                        //    if ((DateTime.Now - lastOutfitSelectTime).TotalMilliseconds > 600)
                        //    {
                        //        // If no category selected yet, select the hovered menu category
                        //        if (selectedMenuCategory < 0)
                        //        {
                        //            selectedMenuCategory = menuHoverIndex;
                        //            Console.WriteLine("Selected category: " + selectedMenuCategory);
                        //        }
                        //        // If a category is already selected, confirm the current item in that category
                        //        else
                        //        {
                        //            int catIdx = selectedMenuCategory;
                        //            int itemIdx = Math.Abs(scrollIndices[catIdx]) % items[catIdx].Length;
                        //            string selectedItem = items[catIdx][itemIdx];

                        //            // TOPS: Shirts (0), Hoodies (1), Jackets (2)
                        //            if (catIdx <= 2)
                        //            {
                        //                currentTop = selectedItem;
                        //            }
                        //            // BOTTOMS: Pants (3), Shorts (4)
                        //            else
                        //            {
                        //                currentBottom = selectedItem;
                        //            }

                        //            hoodieColor = selectedItem;
                        //            Console.WriteLine("SELECTED: " + selectedItem + " | Top=" + currentTop + " | Bottom=" + currentBottom);
                        //        }
                        //        lastOutfitSelectTime = DateTime.Now;
                        //    }
                        //}
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
                                    Console.WriteLine(String.Format("OUTFIT BUILDER: Added 1 to {0}, total: {1}", hoodieColor, cart[hoodieColor]));
                                    hoodieCount = DateTime.Now;
                                }
                                // Minus button (ID 4) - only if quantity > 0
                                else if (tobj.SymbolID == 4 && cart[hoodieColor] > 0)
                                {
                                    cart[hoodieColor]--;
                                    Console.WriteLine(String.Format("OUTFIT BUILDER: Removed 1 from {0}, total: {1}", hoodieColor, cart[hoodieColor]));
                                    hoodieCount = DateTime.Now;
                                }
                            }
                            else
                            {
                                Console.WriteLine(String.Format("OUTFIT BUILDER: hoodieColor={0} not in cart or empty", hoodieColor));
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
                                Console.WriteLine(String.Format("Added {0} to cart", currentTop));
                                // Set hoodieColor to the added item for selection
                                hoodieColor = currentTop;
                            }

                            // Add current BOTTOM to cart
                            if (!string.IsNullOrEmpty(currentBottom))
                            {
                                if (!cart.ContainsKey(currentBottom)) cart[currentBottom] = 0;
                                cart[currentBottom]++;
                                Console.WriteLine(String.Format("Added {0} to cart", currentBottom));
                            }

                            hoodieSwitch = DateTime.Now;
                        }
                    }

                    // === OUTFIT BUILDER DELETE ITEM (ID 12) ===
                    if (tobj.SymbolID == 12 && outfitbuilder)
                    {
                        if ((DateTime.Now - hoodieSwitch).TotalMilliseconds > 500)
                        {
                            if (!string.IsNullOrEmpty(hoodieColor) && cart.ContainsKey(hoodieColor) && cart[hoodieColor] > 0)
                            {
                                Console.WriteLine(String.Format("OUTFIT BUILDER: Deleting {0} from cart and outfit", hoodieColor));

                                // Remove from outfit display so it won't be added again
                                if (currentTop == hoodieColor)
                                {
                                    currentTop = "";
                                    Console.WriteLine(String.Format("Removed {0} from currentTop", hoodieColor));
                                }
                                if (currentBottom == hoodieColor)
                                {
                                    currentBottom = "";
                                    Console.WriteLine(String.Format("Removed {0} from currentBottom", hoodieColor));
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

                                Console.WriteLine(String.Format("Selected cart item: {0} (index {1})", hoodieColor, currentIdx));
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
                    if (tobj.SymbolID == 7)
                    {
                        // Home page: select between Bestsellers, Deals, Outfit Builder
                        if (home)
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
                        // Login page: select between Login and Signup
                        else if (login)
                        {
                            if ((DateTime.Now - homeSwitchTime).TotalMilliseconds > 500)
                            {
                                if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 90)
                                {
                                    selectedLoginCard = (selectedLoginCard + 1) % 2;
                                    homeSwitchTime = DateTime.Now;
                                }
                                else if (tobj.AngleDegrees > 270 && tobj.AngleDegrees < 340)
                                {
                                    selectedLoginCard = (selectedLoginCard - 1 + 2) % 2;
                                    homeSwitchTime = DateTime.Now;
                                }
                            }
                        }
                        // Bestsellers/Deals pages: scroll and select items
                        else if (bestsellers || deals)
                        {
                            if ((DateTime.Now - lastScrollTime).TotalMilliseconds > 450)
                            {
                                string[] currentList = deals ?
                                    new string[] { "Navy Shirt", "Black Hoodie", "Denim Pants", "Black Jacket", "Pink Hoodie", "Burgundy Shirt", "Black Pants", "Denim Jacket" } :
                                    new string[] { "Navy Shirt", "Black Hoodie", "Denim Pants", "Black Jacket", "Pink Hoodie", "Burgundy Shirt", "Black Pants", "Denim Jacket" };

                                bool changed = false;

                                // Rotate Right - move selection right, scroll only when at rightmost card (index 3)
                                if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 120)
                                {
                                    if (selectionIndex < 3)
                                    {
                                        // Move selection to the right
                                        selectionIndex++;
                                        changed = true;
                                    }
                                    else
                                    {
                                        // Selection is at rightmost card, scroll the list
                                        scrollIndex = (scrollIndex + 1) % currentList.Length;
                                        changed = true;
                                    }
                                }
                                // Rotate Left - move selection left, scroll only when at leftmost card (index 0)
                                else if (tobj.AngleDegrees > 240 && tobj.AngleDegrees < 340)
                                {
                                    if (selectionIndex > 0)
                                    {
                                        // Move selection to the left
                                        selectionIndex--;
                                        changed = true;
                                    }
                                    else
                                    {
                                        // Selection is at leftmost card, scroll the list
                                        scrollIndex = (scrollIndex - 1 + currentList.Length) % currentList.Length;
                                        changed = true;
                                    }
                                }

                                if (changed)
                                {
                                    // Update hoodieColor to the currently selected item
                                    int selectedItemIndex = (scrollIndex + selectionIndex) % currentList.Length;
                                    hoodieColor = currentList[selectedItemIndex];
                                    lastScrollTime = DateTime.Now;
                                }
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
                            selectionIndex = 0;
                            selectedMenuCategory = -1;
                            menuHoverIndex = 0;

                            Console.WriteLine("Returning to Home Page...");
                        }
                        // Return from LoginSteps or SignupSteps back to Login page
                        else if (loginsteps || signupsteps)
                        {
                            loginsteps = false;
                            signupsteps = false;
                            login = true;
                            Console.WriteLine("Returning to Login Page...");
                        }
                    }


                    if (outfitbuilder)
                    {
                        // --- STEP A: ROTATE MENU HOVER (ID 9) ---
                        if (tobj.SymbolID == 9)
                        {
                            if ((DateTime.Now - lastMenuRotateTime).TotalMilliseconds > 500)
                            {
                                if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 120)
                                {
                                    menuHoverIndex = (menuHoverIndex + 1) % 5;
                                    lastMenuRotateTime = DateTime.Now;
                                }
                                else if (tobj.AngleDegrees > 240 && tobj.AngleDegrees < 340)
                                {
                                    menuHoverIndex = (menuHoverIndex - 1 + 5) % 5;
                                    lastMenuRotateTime = DateTime.Now;
                                }
                            }
                        }

                        // --- STEP B: SELECT CATEGORY (ID 8) ---
                        // When ID 8 is placed, it "locks in" the category you were hovering over
                        if (tobj.SymbolID == 8)
                        {
                            selectedMenuCategory = menuHoverIndex;
                            Console.WriteLine("Locked Category: " + selectedMenuCategory);
                        }

                        // --- STEP C: UNIVERSAL SCROLLING (ID 7 ONLY) ---
                        if (tobj.SymbolID == 7 && selectedMenuCategory >= 0)
                        {
                            if ((DateTime.Now - lastScrollTime).TotalMilliseconds > 800)
                            {
                                int cat = selectedMenuCategory;
                                int oldIdx = scrollIndices[cat];

                                if (tobj.AngleDegrees > 20 && tobj.AngleDegrees < 120) // Rotate Right
                                    scrollIndices[cat] = (scrollIndices[cat] + 1) % items[cat].Length;
                                else if (tobj.AngleDegrees > 240 && tobj.AngleDegrees < 340) // Rotate Left
                                    scrollIndices[cat] = (scrollIndices[cat] - 1 + items[cat].Length) % items[cat].Length;

                                if (oldIdx != scrollIndices[cat])
                                {
                                    lastScrollTime = DateTime.Now;

                                    // --- THIS FIXES THE DISPLAY ---
                                    // We update the 'worn' items IMMEDIATELY as you scroll
                                    string newItem = items[cat][scrollIndices[cat]];
                                    if (cat <= 2) currentTop = newItem; // Shirts, Hoodies, Jackets
                                    else currentBottom = newItem;      // Pants, Shorts

                                    hoodieColor = newItem; // Focus for quantity
                                }
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

        // Always-on emotion badge (drawn outside any TUIO-object conditional so it shows on every page)
        if (_adaptive != null && _adaptive.FaceDetected)
        {
            string badge = "😊 " + _adaptive.RawEmotion + " (" + _adaptive.Confidence.ToString("P0") + ")";
            using (var bf = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var bb = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
            {
                var sz = g.MeasureString(badge, bf);
                g.FillRectangle(bb, 8, 8, sz.Width + 12, sz.Height + 6);
                g.DrawString(badge, bf, Brushes.White, 14, 11);
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

    private void UpdateBluetoothSigninState()
    {
        DateTime now = DateTime.Now;

        if (!login)
        {
            bluetoothSignedInAt = DateTime.MinValue;
            bluetoothIdentity = "";
            return;
        }

        if ((now - lastBluetoothPollTime) >= bluetoothPollInterval)
        {
            lastBluetoothPollTime = now;
            ReadBluetoothStateFile();
        }

        if (bluetoothSigninStatus == "signed_in" && !string.IsNullOrWhiteSpace(bluetoothUsername))
        {
            string currentIdentity = bluetoothUsername + "|" + bluetoothMac;
            if (currentIdentity != bluetoothIdentity)
            {
                bluetoothIdentity = currentIdentity;
                bluetoothSignedInAt = now;
            }

            if (bluetoothSignedInAt != DateTime.MinValue && (now - bluetoothSignedInAt) >= loginAutoReturnDelay)
            {
                login = false;
                loginsteps = false;
                signupsteps = false;
                home = true;
            }
        }
        else
        {
            bluetoothSignedInAt = DateTime.MinValue;
            bluetoothIdentity = "";
        }
    }

    private void ReadBluetoothStateFile()
    {
        bluetoothSigninStatus = "searching";
        bluetoothUsername = "";
        bluetoothMac = "";
        loginStatusMessage = "Searching for recently connected Bluetooth device...";

        if (!File.Exists(bluetoothStatePath))
        {
            return;
        }

        try
        {
            string content = File.ReadAllText(bluetoothStatePath);
            string status = ExtractJsonStringValue(content, "status");
            string username = ExtractJsonStringValue(content, "username");
            string mac = ExtractJsonStringValue(content, "mac");
            string reason = ExtractJsonStringValue(content, "selection_reason");

            if (!string.IsNullOrWhiteSpace(status))
            {
                bluetoothSigninStatus = status.Trim().ToLowerInvariant();
            }

            bluetoothUsername = username.Trim();
            bluetoothMac = mac.Trim();

            if (bluetoothSigninStatus == "signed_in" && !string.IsNullOrWhiteSpace(bluetoothUsername))
            {
                loginStatusMessage = "Detected user: " + bluetoothUsername;
            }
            else if (bluetoothSigninStatus == "error")
            {
                loginStatusMessage = "Bluetooth unavailable. Waiting for device...";
            }
            else if (bluetoothSigninStatus == "login_required")
            {
                // Python watcher: no allowed device currently connected (disconnect or not paired)
                loginStatusMessage = "Bluetooth disconnected — connect your device to sign in.";
            }
            else if (bluetoothSigninStatus == "searching")
            {
                loginStatusMessage = "Bluetooth adapter off or unavailable. Turn Bluetooth on.";
            }
            else if (!string.IsNullOrWhiteSpace(reason))
            {
                loginStatusMessage = "Searching for recently connected Bluetooth device...";
            }
        }
        catch (Exception ex)
        {
            loginStatusMessage = "Searching for recently connected Bluetooth device...";
            Console.WriteLine("Bluetooth state read error: " + ex.Message);
        }
    }

    private static string ExtractJsonStringValue(string content, string key)
    {
        try
        {
            string pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"(?<v>(?:\\\\.|[^\\\"])*)\"";
            Match m = Regex.Match(content, pattern, RegexOptions.CultureInvariant);
            if (!m.Success)
            {
                return "";
            }
            return Regex.Unescape(m.Groups["v"].Value);
        }
        catch
        {
            return "";
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

    private static string ResolveBluetoothStatePath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string projectState = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".runtime", "current_user.json"));

        if (File.Exists(projectState) || Directory.Exists(Path.GetDirectoryName(projectState)))
        {
            return projectState;
        }

        string localState = Path.Combine(baseDir, ".runtime", "current_user.json");
        return localState;
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