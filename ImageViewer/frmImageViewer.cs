using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ImageViewer
{
    public partial class frmImageViewer : Form
    {
        private readonly ISettingsStorage _settingsStorage;
        private readonly Settings _settings;

        /// <summary>
        /// Current directory
        /// </summary>
        private string _directoryName;

        /// <summary>
        /// List of files in the current directory
        /// </summary>
        private string[] _files;

        /// <summary>
        /// Current file
        /// </summary>
        private int _fileIndex;

        private string currentFile => _files[_fileIndex];
        public bool ShouldOpenAgain { get; private set; } = false;

        public frmImageViewer(string fileName, ISettingsStorage settingsStorage, Settings settings)
        {
            _settingsStorage = settingsStorage;
            _settings = settings;
            InitializeComponent();
            Opacity = 0;
            ImageBox.DefaultZoom = settings.DefaultZoom;
            
            var iconFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "icon.ico");
            try
            {
                Icon = new Icon(iconFile);
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Error loading icon from {iconFile} : {ex.Message}");
            }
            ImageBox.DoubleClick += ImageBox_DoubleClick;

            //this.KeyPreview = true;
            LoadImage(fileName);
        }

        /// <summary>
        /// On first shown, focus the window.
        /// Could be unfocused if an OpenFileDialog was first opened.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateImage(1);
            if (_settings.StartInFullScreen)
            {
                ToggleFullScreen(Screen.FromPoint(Cursor.Position));
            }
            Activate();
            BringToFront();
            Opacity = 1;
            ImageBox.ShowMessage("Press H for help", 1000);
        }

        private void ImageBox_DoubleClick(object sender, EventArgs e)
        {
            ToggleFullScreen();
        }

        /// <summary>
        /// Load an image from disk
        /// </summary>
        /// <param name="fileName"></param>
        protected void LoadImage(string fileName)
        {
            _directoryName = Path.GetDirectoryName(fileName);

            // Find all the images in the same directory as fileName
            _files = Program.Extensions.SelectMany(ext =>
                        Directory.GetFiles(_directoryName, ext))
                        .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToArray();

            // Find the index of fileName in pFiles
            for (int i = 0; i < _files.Length; ++i)
            {
                if (_files[i].Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    _fileIndex = i;
                    break;
                }
            }
        }

        private Timer _autoPlayTimer;

        private void AutoPlay()
        {
            if(_autoPlayTimer == null)
            {
                _autoPlayTimer = new Timer
                {
                    Interval = 2000
                };
                _autoPlayTimer.Tick += (s, e) =>
                {
                    NextImage();
                };
            }
            if(_autoPlayTimer.Enabled)
            {
                _autoPlayTimer.Stop();
                ImageBox.ShowMessage("Autoplay OFF", 900);
            }
            else
            {
                _autoPlayTimer.Start();
                ImageBox.ShowMessage("Autoplay ON", 900);
            }
        }

        private double _transparency = 0.3f;

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

                case Keys.S:
                    Shuffle();
                    return true;
                
                // [F]: Toggle fullscreen
                case Keys.F:
                    ToggleFullScreen();
                    return true;
                
                // [Esc] close topmost -> quit
                case Keys.Escape:
                    if (TopMost)
                    {
                        ToggleFullScreen();
                    }
                    else
                    {
                        Close();
                    }
                    return true;

                // [H]: Show help text
                case Keys.H:
                    if (ImageBox.HasMessage)
                    {
                        ImageBox.HideMessage();
                    }
                    else
                    {
                        ImageBox.ShowMessage(Program.GetResource("helptext.txt"), 10000);
                    }
                    return true;

                // [Q]: Quit
                case Keys.Q:
                    Close();
                    return true;

                // [O]: ReOpen
                case Keys.O:
                    ShouldOpenAgain = true;
                    Close();
                    return true;
                
                case Keys.P:
                    ShowPreferences();
                    break;

                // [T] Toggle topmost
                case Keys.T:
                    TopMost = !TopMost; 
                    ImageBox.ShowMessage($"Now {(TopMost ? "" : "not ")}Top most", 300);
                    return true;

                case Keys.W:
                    ToggleClickThrough();
                    break;

                case Keys.D2:
                    _transparency = .1;
                    break;
                case Keys.D3:
                    _transparency = .2;
                    break;
                case Keys.D4:
                    _transparency = .3;
                    break;
                case Keys.D5:
                    _transparency = .5;
                    break;
                case Keys.D6:
                    _transparency = .75;
                    break;

                // 1 : Switch between 100% and best fit
                case Keys.D1:
                    if(ImageBox.Zoom != 1)
                    {
                        ImageBox.Zoom = 1;
                    }
                    else
                    {
                        ImageBox.Zoom = 0;
                        ImageBox.ResetTransform();
                    }
                    return true;
                // [B]	Toggle borderless
                case Keys.B:
                    ToggleBorderless();
                    return true;

                // [C]	Copy file name
                case Keys.C:
                    Clipboard.SetText(currentFile);
                    ImageBox.ShowMessage($"File name copied to clipboard", 300);
                    return true;
                // [A] Toggle autoplay
                case Keys.A:
                    AutoPlay();
                    return true;
                // [+] Autoplay faster
                case Keys.Add:
                    if(_autoPlayTimer!=null)
                    {
                        _autoPlayTimer.Interval =(int) ((float)_autoPlayTimer.Interval * 0.9f);
                        ImageBox.ShowMessage($"Autoplay interval: {_autoPlayTimer.Interval}ms", 300);
                    }
                    break;
                // [-] Autoplay slower
                case Keys.Subtract:
                    if (_autoPlayTimer != null)
                    {
                        _autoPlayTimer.Interval = (int)((float)_autoPlayTimer.Interval * 1.112f);
                        ImageBox.ShowMessage($"Autoplay interval: {_autoPlayTimer.Interval}ms", 300);
                    }
                    break;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ShowPreferences()
        {
            if(TopMost)
            {
                ToggleFullScreen();
            }

            var form = new frmSettings(_settingsStorage, _settings);
            if (form.ShowDialog() == DialogResult.OK)
            {
                ImageBox.DefaultZoom = _settings.DefaultZoom;
            }
        }

        private void Shuffle()
        {
            var r = new Random((int)DateTime.Now.ToFileTime());
            _files = _files.OrderBy(x => r.Next()).ToArray();
        }

        private bool _isClickThrough = false;

        private void ToggleClickThrough()
        {
            _isClickThrough = !_isClickThrough;
            if(_isClickThrough)
            {
                ImageBox.ShowMessage($"Window will be click-thru once out of focus", 1000);
            }
            else
            {
                ImageBox.ShowMessage($"Click-thru disabled", 500);
            }
        }

        private void ToggleBorderless()
        {
            if (TopMost)
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                TopMost = false;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.None;
                TopMost = true;
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            Opacity = 1;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            try {
                if (_settings.TransparentWhenOutOfFocus || _isClickThrough)
                {
                    Opacity = _transparency;

                    if (_isClickThrough)
                    {
                        var handle = new HandleRef(this, Handle);
                        var initialStyle = InteropHelper.GetWindowLongPtr(handle, (int)InteropHelper.GWL.ExStyle)
                            .ToInt64();
                        const long layeredTransparent =
                            (long)InteropHelper.WS_EX.Layered | (long)InteropHelper.WS_EX.Transparent;
                        var newStyle = initialStyle | layeredTransparent;
                        var result = InteropHelper.SetWindowLongPtr(handle, (int)InteropHelper.GWL.ExStyle,
                            new IntPtr(newStyle));
                    }
                }
            }
            catch { }
        }

        private void ToggleFullScreen(Screen which = null)
        {
            if (TopMost)
            {
                TopMost = false;
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                var screen = which ?? Screen.FromControl(this);
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                TopMost = true;
                Location = screen.Bounds.Location;
                Size = screen.Bounds.Size;
            }
        }

        private void RotateImageInv()
        {
            ImageBox.Rotation += 90;
        }

        private void RotateImage()
        {
            ImageBox.Rotation -= 90;
        }

        private void PreviousImage()
        {
            _fileIndex--;
            if (_fileIndex < 0)
                _fileIndex = _files.Length - 1;
            UpdateImage(-1);
        }

        private void NextImage()
        {
            _fileIndex++;
            if (_fileIndex >= _files.Length)
                _fileIndex = 0;
            UpdateImage(1);
        }

        /// <summary>
        /// Cache timer, starts caching when the user is idle.
        /// </summary>
        private Timer _timerCache = null;
        private int _lastDirection = 0;
        private int _cachedCount = 0;
        //private long pLastImageSwitchTicks = 0;

        private void UpdateImage(int direction)
        {
            _lastDirection = direction;
            // Set up timer
            if (_timerCache == null)
            {
                _timerCache = new Timer
                {
                    Interval = 300
                };
                _timerCache.Tick += (sender, args) =>
                {
                    if (_lastDirection != 0)
                    {
                        int iNext = _fileIndex + _lastDirection;
                        if (iNext == _files.Length)
                            iNext = 0;
                        if (iNext < 0)
                            iNext = _files.Length - 1;  
                    }
                    if(_cachedCount >= 3)
                    {
                        _timerCache.Stop();
                    }
                    else
                    {
                        _cachedCount++;
                    }
                };
            }

            // Stop the cache timer
            _timerCache.Stop();

            Text = string.Format("{0} - Image Viewer", Path.GetFileName(currentFile));
            ImageBox.LoadImage(currentFile);

            // Start caching timer 
            _cachedCount = 0;
            _timerCache.Start();
        }
    }
}
