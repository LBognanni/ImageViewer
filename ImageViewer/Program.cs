﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer
{
    static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        public static string[] Extensions = {
     "*.bmp",
     "*.dds",
     "*.exr",
     "*.gif",
     "*.hdr",
     "*.ico",
     "*.iff",
     "*.jbig",
     "*.jng",
     "*.jpeg",
     "*.jif",
     "*.jpg",
     "*.koala",
     "*.mng",
     "*.pcx",
     "*.pbm",
     "*.pgm",
     "*.ppm",
     "*.pfm",
     "*.png",
     "*.pict",
     "*.psd",
     "*.raw",
     "*.ras",
     "*.sgi",
     "*.tga",
     "*.tiff",
     "*.tif",
     "*.wbmp",
     "*.webp",
     "*.xbm",
     "*.xpm"
    };

        class FileOpenContext : ApplicationContext
        {
            private readonly ISettingsStorage _settingsStorage;
            private readonly Settings _settings;
            private readonly ITempData _tempData;
            private frmImageViewer? _frmImageViewer;
            public bool ShouldCloseImmediately { get; set; }

            public FileOpenContext(string fileName, ISettingsStorage settingsStorage, Settings settings, ITempData tempData)
            {
                _settingsStorage = settingsStorage;
                _settings = settings;
                _tempData = tempData;
                ShowForm(fileName);
            }

            private void ShowForm(string fileName)
            {
                fileName = GetFileToShow(fileName);
                if (!string.IsNullOrEmpty(fileName))
                {
                    _frmImageViewer = new frmImageViewer(fileName, _settingsStorage, _settings, _tempData);
                    _frmImageViewer.Location = Cursor.Position;
                    _frmImageViewer.FormClosed += _frmImageViewer_FormClosed;
                    _frmImageViewer.Show();
                }
                else
                {
                    ShouldCloseImmediately = true;
                }
            }

            private string GetFileToShow(string fileName)
            {
                if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
                {
                    return fileName;
                }

                var dlg = new OpenFileDialog
                {
                    Filter = "Image file |" + string.Join(";", Extensions),
                    Title = "Select an image file"
                };

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    return dlg.FileName;
                }

                return "";
            }

            private void _frmImageViewer_FormClosed(object sender, FormClosedEventArgs e)
            {
                if (_frmImageViewer?.ShouldOpenAgain ?? false)
                {
                    ShowForm("");
                }
                else
                {
                    ExitThread();
                }
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Fix UHD displays
            if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware();

            var fileName = "";
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var tempData = new FileBasedTempData();

            try
            {
                var checker = new VersionChecker(new GitHubVersionGetter(),
                    Assembly.GetExecutingAssembly().GetName().Version, new WindowsNotification(),
                    tempData);
                Task.Run(() => checker.NotifyIfNewVersion());
            }
            catch(Exception e)
            {
                Debug.WriteLine($"Can't check new version: {e.Message}.");
            }

            if (args.Length > 0)
            {
                if (File.Exists(args[0]))
                {
                    fileName = args[0];
                }
            }

            var settingsStorage = new SettingsJsonStorage();
            Settings settings;
            try
            {
                settings = settingsStorage.LoadSettings().Result;
            }
            catch (Exception ex)
            {
                if (MessageBox.Show(
                        $"Unable to load settings. Load default settings instead?\r\nThis will overwrite any custom changes to your settings script.\r\n\r\nError: {GetErrorMessage(ex)}",
                        "Image Viewer", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    settings = new Settings();
                }
                else
                {
                    return;
                }
            }
            settingsStorage.SaveSettings(settings).Wait();

            using var ctx = new FileOpenContext(fileName, settingsStorage, settings, tempData);
            if (!ctx.ShouldCloseImmediately)
            {
                Application.Run(ctx);
            }
        }

        private static string GetErrorMessage(Exception exception)
        {
            if (exception.InnerException != null)
                return GetErrorMessage(exception.InnerException);
            return exception.Message;
        }
    }
}
