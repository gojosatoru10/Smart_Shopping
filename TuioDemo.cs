
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
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

    /// State variables for the different pages of the application, allowing for easy switching between them based on user interactions with the TUIO objects.
    public bool home = true, login = false, clothes = false, checkout = false, dark = false, thankyou=false;

    /// Represents the root file system path for assets.
    private readonly string assetRootPath;

    /// Represents the current theme path, which can be switched between Light and Dark themes based on user interactions.
    public string themePath;


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

    public int cthoodieBlack = 0;
    public int cthoodieGrey = 0;
    public int cthoodieBurgundy = 0;
    public int cthoodiePink = 0;

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
            Bitmap img = new Bitmap(Path.Combine(themePath, "Home.png"));
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
            Bitmap img = new Bitmap(Path.Combine(themePath, "Login.png"));
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
            Bitmap img = new Bitmap(Path.Combine(themePath, $"Select{hoodieColor}.png"));
            ResizeImage(ref img);
            Display_Current_Page(img);

            // Draw the count of each hoodie centered between the + and - buttons
            using (Font countFont = new Font("Arial", 28f, FontStyle.Bold))
            using (SolidBrush countBrush = new SolidBrush(Color.White))
            {
                // Use proportional coordinates so it works at any resolution
                float cw = ClientSize.Width;
                float ch = ClientSize.Height;
                float countY = ch * 0.820f;
                g.DrawString(cthoodieBlack.ToString(), countFont, countBrush, cw * 0.157f, countY);
                g.DrawString(cthoodieGrey.ToString(), countFont, countBrush, cw * 0.386f, countY);
                g.DrawString(cthoodieBurgundy.ToString(), countFont, countBrush, cw * 0.617f, countY);
                g.DrawString(cthoodiePink.ToString(), countFont, countBrush, cw * 0.846f, countY);
            }





        }
        if (clothes == true)
        {
            DrawClothesScreen();
        }
        ///


        /// Draws The Checkout Screen
        void DrawCheckoutScreen()
        {
            Bitmap img = new Bitmap(Path.Combine(themePath, $"Checkout{hoodieColor}.png"));
            ResizeImage(ref img);
            Display_Current_Page(img);
        }
        if (checkout == true)
        {
            DrawCheckoutScreen();
        }

        void DrawThankyouScreen()
        {
            Bitmap img = new Bitmap(Path.Combine(themePath, "ThankYou.png"));
            ResizeImage(ref img);
            Display_Current_Page(img);
        }
        if (thankyou == true)
        {
            DrawThankyouScreen();
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
                    if (tobj.SymbolID == 2&&clothes)
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

                    /// Handles the logic for proceeding to checkout when the object with SymbolID 5 is rotated, ensuring that the user has selected a hoodie color and quantity before allowing them to move to the checkout page.
                    if (tobj.SymbolID == 5 && clothes)
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

