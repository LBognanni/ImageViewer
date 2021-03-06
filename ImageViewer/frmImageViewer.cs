﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer
{
    public partial class frmImageViewer : Form
    {
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

        public frmImageViewer(string fileName)
        {
            InitializeComponent();
            var iconFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "icon.ico");
            try
            {
                this.Icon = new Icon(iconFile);
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Error loading icon from {iconFile} : {ex.Message}");
            }
            ImageBox.DoubleClick += ImageBox_DoubleClick;

            //this.KeyPreview = true;
            LoadImage(fileName);
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
            _directoryName = System.IO.Path.GetDirectoryName(fileName);

            // Find all the images in the same directory as fileName
            _files = Program.Extensions.SelectMany(ext =>
                        System.IO.Directory.GetFiles(_directoryName, ext))
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

        /// <summary>
        /// On first shown, focus the window.
        /// Could be unfocused if an OpenFileDialog was first opened.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.Activate();
            this.BringToFront();
            UpdateImage(1);
            ImageBox.ShowMessage("Press H for help", 1000);
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
                    this.Close();
                    return true;

                // [T] Toggle topmost
                case Keys.T:
                    this.TopMost = !this.TopMost;
                    return true;

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
                    return true;
                // [A] Toggle autoplay
                case Keys.A:
                    AutoPlay();
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

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
                        ImageBox.CacheImage(_files[iNext]);
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

            this.Text = string.Format("{0} - Image Viewer", System.IO.Path.GetFileName(currentFile));
            ImageBox.LoadImage(currentFile);

            // Start caching timer 
            _cachedCount = 0;
            _timerCache.Start();
        }
    }
}
