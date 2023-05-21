using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageViewer
{
    public class ImageMeta : IDisposable
    {
        public ImageMeta(Bitmap bmp)
        {
            Image = bmp;
        }
        public Bitmap Image { get; }
        public Color AverageColor { get; set; }
        public string FileName { get; set; } = "";
        public bool IsFullResolution { get; set; }
        public int ActualWidth { get; set; }
        public int ActualHeight { get; set; }
        public int LastUsed { get; set; }

        public void Dispose()
        {
            Image.Dispose();
        }
    }
}
