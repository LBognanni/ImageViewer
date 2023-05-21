using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

namespace ImageViewer
{
    public partial class frmImageViewer : Form
    {
        private readonly ISettingsStorage _settingsStorage;
        private readonly Settings _settings;
        private readonly ITempData _tempData;

        /// <summary>
        /// Current directory
        /// </summary>
        private string _directoryName = "";

        /// <summary>
        /// List of files in the current directory
        /// </summary>
        private List<string> _files;

        /// <summary>
        /// Current file
        /// </summary>
        private int _fileIndex;

        private string CurrentFile => _files[_fileIndex];
        public bool ShouldOpenAgain { get; private set; } = false;
        private readonly (string, string)[] _actionDescriptions;

        public frmImageViewer(string fileName, ISettingsStorage settingsStorage, Settings settings, ITempData tempData)
        {
            _settingsStorage = settingsStorage;
            _settings = settings;
            _tempData = tempData;
            _files = new List<string>();

            _defaultActions = new Dictionary<string, Action>()
            {
                { nameof(AutoPlay), AutoPlay },
                { nameof(AutoPlayFaster), AutoPlayFaster },
                { nameof(ToggleBorderless), ToggleBorderless },
                { nameof(CopyFileName), CopyFileName },
                { nameof(ToggleZoom), ToggleZoom },
                { nameof(Transparency1), Transparency1 },
                { nameof(Transparency2), Transparency2 },
                { nameof(Transparency3), Transparency3 },
                { nameof(Transparency4), Transparency4 },
                { nameof(Transparency5), Transparency5 },
                { nameof(ExitFullScreenOrClose), ExitFullScreenOrClose },
                { nameof(ToggleFullScreen), ToggleFullScreen },
                { nameof(ToggleHelpMessage), ToggleHelpMessage },
                { nameof(PreviousImage), PreviousImage },
                { nameof(ReOpen), ReOpen },
                { nameof(ShowPreferences), ShowPreferences },
                { nameof(Close), Close },
                { nameof(RotateImage), RotateImage },
                { nameof(RotateImageInv), RotateImageInv },
                { nameof(NextImage), NextImage },
                { nameof(Shuffle), Shuffle },
                { nameof(AutoPlaySlower), AutoPlaySlower },
                { nameof(ToggleTopMost), ToggleTopMost },
                { nameof(ToggleClickThrough), ToggleClickThrough },
                { nameof(DeleteCurrentFile), DeleteCurrentFile }
            };
            _actionDescriptions = new[]
            {
                ( nameof(AutoPlay), "Toggle Autoplay" ),
                ( nameof(AutoPlayFaster), "Autoplay Faster" ),
                ( nameof(AutoPlaySlower), "Autoplay Slower" ),
                ( nameof(ToggleZoom), "Toggle Zoom" ),
                ( nameof(CopyFileName), "Copy File Name" ),
                ( nameof(PreviousImage), "Previous Image" ),
                ( nameof(NextImage), "Next Image" ),
                ( nameof(ToggleBorderless), "Toggle Borderless" ),
                ( nameof(ToggleFullScreen), "Toggle FullScreen" ),
                ( nameof(ToggleTopMost), "Toggle TopMost" ),
                ( nameof(ToggleClickThrough), "Toggle ClickThrough" ),
                ( nameof(Transparency1), "Transparency: 1/5" ),
                ( nameof(Transparency2), "Transparency: 2/5" ),
                ( nameof(Transparency3), "Transparency: 3/5" ),
                ( nameof(Transparency4), "Transparency: 4/5" ),
                ( nameof(Transparency5), "Transparency: 5/5" ),
                ( nameof(ReOpen), "Show the Open dialog" ),
                ( nameof(ShowPreferences), "Show Preferences" ),
                ( nameof(RotateImage), "Rotate Image clockwise" ),
                ( nameof(RotateImageInv), "Rotate Image counter-clockwise" ),
                ( nameof(Shuffle), "Shuffle" ),
                ( nameof(DeleteCurrentFile), "Delete Current File"),
                ( nameof(Close), "Quit" ),
                ( nameof(ToggleHelpMessage), "Show this help message" ),
                //( nameof(ExitFullScreenOrClose), ExitFullScreenOrClose ),
            };
            
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
            _directoryName = Path.GetDirectoryName(fileName)!;

            // Find all the images in the same directory as fileName
            _files = Program.Extensions.SelectMany(ext =>
                        Directory.GetFiles(_directoryName, ext))
                        .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToList();

            // Find the index of fileName in pFiles
            for (int i = 0; i < _files.Count; ++i)
            {
                if (_files[i].Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    _fileIndex = i;
                    break;
                }
            }
        }

        private Timer? _autoPlayTimer;

        private Dictionary<string, Action> _defaultActions;

        private void AutoPlay()
        {
            if(_autoPlayTimer == null)
            {
                _autoPlayTimer = new Timer
                {
                    Interval = 2000
                };
                _autoPlayTimer.Tick += (_, _) =>
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
            if (_settings.TryGetKeyBindingFor(keyData, out var keyBinding))
            {
                if (!string.IsNullOrEmpty(keyBinding.Action) && _defaultActions.TryGetValue(keyBinding.Action!, out var action))
                {
                    action();
                    return true;
                }
                else if (!string.IsNullOrEmpty(keyBinding.Command))
                {
                    var command = string.Format(keyBinding.Command!, _files[_fileIndex]);
                    try
                    {
                        var (exe, parameters) = FileUtilites.SplitCommand(command);
                        Process.Start(exe, parameters);
                        if (keyBinding.IsDeleteAction)
                        {
                            RemoveFromIndex();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Cannot run custom command: {ex.Message}.\r\n The command ran was:\r\n{command}", this.Text, MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RemoveFromIndex()
        {
            _files.RemoveAt(_fileIndex);
            _fileIndex--;
            NextImage();
        }

        private void Transparency5()
        {
            _transparency = .75;
        }

        private void Transparency4()
        {
            _transparency = .5;
        }

        private void Transparency3()
        {
            _transparency = .3;
        }

        private void Transparency2()
        {
            _transparency = .2;
        }

        private void Transparency1()
        {
            _transparency = .1;
        }

        private void AutoPlaySlower()
        {
            if (_autoPlayTimer != null)
            {
                _autoPlayTimer.Interval = (int)(_autoPlayTimer.Interval * 1.112f);
                ImageBox.ShowMessage($"Autoplay interval: {_autoPlayTimer.Interval}ms", 300);
            }
        }

        private void AutoPlayFaster()
        {
            if (_autoPlayTimer != null)
            {
                _autoPlayTimer.Interval = (int)(_autoPlayTimer.Interval * 0.9f);
                ImageBox.ShowMessage($"Autoplay interval: {_autoPlayTimer.Interval}ms", 300);
            }
        }

        private void CopyFileName()
        {
            Clipboard.SetText(CurrentFile);
            ImageBox.ShowMessage($"File name copied to clipboard", 300);
        }

        private void ToggleZoom()
        {
            if (ImageBox.Zoom != 1)
            {
                ImageBox.Zoom = 1;
            }
            else
            {
                ImageBox.Zoom = 0;
                ImageBox.ResetTransform();
            }
        }

        private void ToggleTopMost()
        {
            TopMost = !TopMost;
            ImageBox.ShowMessage($"Now {(TopMost ? "" : "not ")}Top most", 300);
        }

        private void ReOpen()
        {
            ShouldOpenAgain = true;
            Close();
        }

        private void ToggleHelpMessage()
        {
            if (ImageBox.HasMessage)
            {
                ImageBox.HideMessage();
            }
            else
            {
                ImageBox.ShowMessage(GetHelpText(), 10000);
            }
        }

        private string GetHelpText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var (command, description) in _actionDescriptions)
            {
                var keyBinding = _settings.KeyBindings.First(x => x.Value.Action == command);
                sb.AppendFormat("{0}\t{1}\r\n", FormatKey(keyBinding.Key), description);
            }
            return sb.ToString();
        }

        private string FormatKey(string key)
        {
            if (key.Contains('|'))
            {
                return String.Join("+",
                    key.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(FormatKey));
            }

            string k = key.ToLower() switch
            {
                "shift" => "⇧",
                "left" => "←",
                "right" => "→",
                "add" => "+",
                "subtract" => "-",
                "d1" => "1",
                "d2" => "2",
                "d3" => "3",
                "d4" => "4",
                "d5" => "5",
                "d6" => "6",
                "d7" => "7",
                "d8" => "8",
                "d9" => "9",
                "control" => "CTRL",
                "controlkey" => "CTRL",
                _ => key.ToUpper()
            };

            return $"[{k}]";
        }

        private void ExitFullScreenOrClose()
        {
            if (TopMost)
            {
                ToggleFullScreen();
            }
            else
            {
                Close();
            }
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
            _files = _files.OrderBy(_ => r.Next()).ToList();
        }

        private bool _isClickThrough;

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

        private void DeleteCurrentFile()
        {
            var yesterday = DateTime.Now.AddDays(-1);
            var dt = _tempData.Read<DateTime?>("LastAlertedOnDelete") ?? yesterday;
            if (dt <= yesterday)
            {
                if (MessageBox.Show("Are you sure you want to delete the current file?", this.Text,
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.Cancel)
                {
                    return;
                }
                _tempData.Write("LastAlertedOnDelete", DateTime.Now);
            }
            
            if (_files.Count > 1)
            {
                try
                {
                    FileSystem.DeleteFile(_files[_fileIndex], UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                    RemoveFromIndex();
                }
                catch (OperationCanceledException)
                {
                    // do nothing
                }
                catch (Exception e) 
                {
                    MessageBox.Show($"Can't delete this file: {e.Message}", "Image Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            NextImage();
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
                        InteropHelper.SetWindowLongPtr(handle, (int)InteropHelper.GWL.ExStyle,
                            new IntPtr(newStyle));
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        private void ToggleFullScreen() => ToggleFullScreen(null);
        private void ToggleFullScreen(Screen? which)
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
                _fileIndex = _files.Count - 1;
            UpdateImage(-1);
        }

        private void NextImage()
        {
            _fileIndex++;
            if (_fileIndex >= _files.Count)
                _fileIndex = 0;
            UpdateImage(1);
        }

        /// <summary>
        /// Cache timer, starts caching when the user is idle.
        /// </summary>
        private Timer? _timerCache;
        private int _lastDirection;
        private int _cachedCount;

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
                        if (iNext == _files.Count)
                            iNext = 0;
                        if (iNext < 0)
                            iNext = _files.Count - 1;  
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

            Text = string.Format("{0} - Image Viewer", Path.GetFileName(CurrentFile));
            ImageBox.LoadImage(CurrentFile);

            // Start caching timer 
            _cachedCount = 0;
            _timerCache.Start();
        }
    }
}
