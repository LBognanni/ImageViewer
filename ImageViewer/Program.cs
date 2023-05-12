using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>
        /// Load a resource from disk
        /// </summary>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public static string GetResource(string resourceName)
        {
            var mainAssembly = Assembly.GetExecutingAssembly();
            using var resource = mainAssembly.GetManifestResourceStream($"{mainAssembly.GetName().Name}.{resourceName}");
            using var sr = new StreamReader(resource);
            return sr.ReadToEnd();
        }

        class FileOpenContext : ApplicationContext
        {
            private readonly ISettingsStorage _settingsStorage;
            private readonly Settings _settings;
            private frmImageViewer _frmImageViewer;

            public FileOpenContext(string fileName, ISettingsStorage settingsStorage, Settings settings)
            {
                _settingsStorage = settingsStorage;
                _settings = settings;
                ShowForm(fileName);
            }

            private void ShowForm(string fileName)
            {
                fileName = GetFileToShow(fileName);
                if (!string.IsNullOrEmpty(fileName))
                {
                    _frmImageViewer = new frmImageViewer(fileName, _settingsStorage, _settings);
                    _frmImageViewer.Location = Cursor.Position;
                    _frmImageViewer.FormClosed += _frmImageViewer_FormClosed;
                    _frmImageViewer.Show();
                }
                else
                {
                    ExitThread();
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
                if (_frmImageViewer.ShouldOpenAgain)
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
        static async Task Main(string[] args)
        {
            // Fix UHD displays
            if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware();

            var fileName = "";
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0)
            {
                if (File.Exists(args[0]))
                {
                    fileName = args[0];
                }
            }

            var settingsStorage = new SettingsJsonStorage();
            var settings = await settingsStorage.LoadSettings();

            Application.Run(new FileOpenContext(fileName, settingsStorage, settings));
        }
    }
}
