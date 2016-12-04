using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer2
{
    public partial class frmImageViewer : Form
    {
        /// <summary>
        /// Current directory
        /// </summary>
        private string pDirectoryName;

        /// <summary>
        /// List of files in the current directory
        /// </summary>
        private string[] pFiles;

        /// <summary>
        /// Current file
        /// </summary>
        private int pIndex;

        public frmImageViewer(string fileName)
        {
            InitializeComponent();
            pImageBox.DoubleClick += PImageBox_DoubleClick;

            //this.KeyPreview = true;
            LoadImage(fileName);
        }

        private void PImageBox_DoubleClick(object sender, EventArgs e)
        {
            ToggleFullScreen();
        }

        /// <summary>
        /// Load an image from disk
        /// </summary>
        /// <param name="fileName"></param>
        protected void LoadImage(string fileName)
        {
            pDirectoryName = System.IO.Path.GetDirectoryName(fileName);

            // Find all the images in the same directory as fileName
            pFiles = Program.Extensions.SelectMany(ext =>
                        System.IO.Directory.GetFiles(pDirectoryName, ext))
                        .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToArray();

            // Find the index of fileName in pFiles
            for (int i = 0; i < pFiles.Length; ++i)
            {
                if (pFiles[i].Equals(fileName, StringComparison.CurrentCultureIgnoreCase))
                {
                    pIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// On first shown, focus the window.
        /// Could be unfocused if an OpenFileDialog was first opened.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.Activate();
            this.BringToFront();
            UpdateImage(1);
            pImageBox.ShowMessage("Press H for help", 1000);
        }

        private Timer pTimerAutoPlay;

        private void AutoPlay()
        {
            if(pTimerAutoPlay == null)
            {
                pTimerAutoPlay = new Timer
                {
                    Interval = 2000
                };
                pTimerAutoPlay.Tick += (s, e) =>
                {
                    NextImage();
                };
            }
            if(pTimerAutoPlay.Enabled)
            {
                pTimerAutoPlay.Stop();
                pImageBox.ShowMessage("Autoplay OFF", 900);
            }
            else
            {
                pTimerAutoPlay.Start();
                pImageBox.ShowMessage("Autoplay ON", 900);
            }
        }

        /// <summary>
        /// Hotkeys
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                // Left: previous image
                case Keys.Left:
                    PreviousImage();
                    return true;

                // Right: next image
                case Keys.Right:
                    NextImage();
                    return true;

                // "R": Rotate image
                case Keys.R:
                    RotateImage();
                    return true;
                
                // [shift][R]: Rotate image CCW
                case Keys.R | Keys.Shift:
                    RotateImageInv();
                    return true;
                    
                
                // [F]: Toggle fullscreen
                case Keys.F:
                    ToggleFullScreen();
                    return true;
                
                // [Esc] close topmost -> quit
                case Keys.Escape:
                    if (this.TopMost)
                    {
                        ToggleFullScreen();
                    }
                    else
                    {
                        this.Close();
                    }
                    return true;

                // [H]: Show help text
                case Keys.H:
                    if (pImageBox.HasMessage)
                    {
                        pImageBox.HideMessage();
                    }
                    else
                    {
                        pImageBox.ShowMessage(Program.GetResource("helptext.txt"), 10000);
                    }
                    return true;
                
                // [Q]: Quit
                case Keys.Q:
                    this.Close();
                    return true;

                // [T] Toggle topmost
                case Keys.T:
                    this.TopMost = !this.TopMost;
                    return true;

                // 1 : Switch between 100% and best fit
                case Keys.D1:
                    if(pImageBox.Zoom != 1)
                    {
                        pImageBox.Zoom = 1;
                    }
                    else
                    {
                        pImageBox.Zoom = 0;
                        pImageBox.ResetTransform();
                    }
                    return true;
                // [B]	Toggle borderless
                case Keys.B:
                    ToggleBorderless();
                    return true;

                // [C]	Copy file name
                case Keys.C:
                    Clipboard.SetText(pFiles[pIndex]);
                    return true;
                // [A] Toggle autoplay
                case Keys.A:
                    AutoPlay();
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }





        public enum GWL
        {
            ExStyle = -20
        }

        public enum WS_EX
        {
            Transparent = 0x20,
            Layered = 0x80000
        }

        public enum LWA
        {
            ColorKey = 0x1,
            Alpha = 0x2
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        public static extern int GetWindowLong(IntPtr hWnd, GWL nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        public static extern int SetWindowLong(IntPtr hWnd, GWL nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte alpha, LWA dwFlags);

        
        private void ToggleBorderless()
        {
            if (this.TopMost)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.TopMost = false;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.TopMost = true;
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            this.Opacity = 1;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            try {
                this.Opacity = .3;
            }
            catch { }
        }

        private void ToggleFullScreen()
        {

            if (this.TopMost)
            {
                this.TopMost = false;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                this.TopMost = true;
                this.Location = Screen.PrimaryScreen.Bounds.Location;
                this.Size = Screen.PrimaryScreen.Bounds.Size;
            }
        }

        private void RotateImageInv()
        {
            pImageBox.Rotation += 90;
        }

        private void RotateImage()
        {
            pImageBox.Rotation -= 90;
        }

        private void PreviousImage()
        {
            pIndex--;
            if (pIndex < 0)
                pIndex = pFiles.Length - 1;
            UpdateImage(-1);
        }

        private void NextImage()
        {
            pIndex++;
            if (pIndex >= pFiles.Length)
                pIndex = 0;
            UpdateImage(1);
        }

        /// <summary>
        /// Cache timer, starts caching when the user is idle.
        /// </summary>
        private Timer pTimerCache = null;
        //private long pLastImageSwitchTicks = 0;

        private void UpdateImage(int direction)
        {
            // Set up timer
            if (pTimerCache == null)
            {
                pTimerCache = new Timer
                {
                    Interval = 100
                };
                pTimerCache.Tick += (sender, args) =>
                {
                    int iNext = pIndex + direction;
                    if (iNext == pFiles.Length)
                        iNext = 0;
                    if (iNext < 0)
                        iNext = pFiles.Length - 1;
                    pImageBox.CacheImage(pFiles[iNext]);
                };
            }

            // Stop the cache timer
            pTimerCache.Stop();

            this.Text = string.Format("{0} - Image Viewer", System.IO.Path.GetFileName(pFiles[pIndex]));
            pImageBox.LoadImage(pFiles[pIndex]);

            // Start caching timer 
            pTimerCache.Start();

            /*
            // Stop caching if the user is changing images very quickly
            const long MINIMUM_WAIT_FOR_CACHE = 200 * TimeSpan.TicksPerMillisecond;
            long time = DateTime.Now.Ticks;
            long tDifTicks = 0;
            if(pLastImageSwitchTicks!=0)
            {
                tDifTicks = (time - pLastImageSwitchTicks);
            }
            pLastImageSwitchTicks = time;


            if ((tDifTicks > MINIMUM_WAIT_FOR_CACHE) && (pFiles.Length > 1))
            {
                int iNext = pIndex + 1;
                if (iNext == pFiles.Length)
                    iNext = 0;
                pImageBox.CacheImage(pFiles[iNext]);
            }*/
        }
    }
}
