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

            if (args.Length > 0)
            {
                if (System.IO.File.Exists(args[0]))
                {
                    fileName = args[0];
                }
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "Image file |" + string.Join(";", Extensions), 
                    Title = "Select an image file"
                };

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    fileName = dlg.FileName;
                }
                else
                    return;
            }

            Application.Run(new frmImageViewer(fileName));
        }
    }
}
