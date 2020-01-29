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
        public Bitmap Image { get; set; }
        public Color AverageColor { get; set; }
        public string FileName { get; set; }
        public bool IsFullResImage { get; set; }
        public int ActualWidth { get; set; }
        public int ActualHeight { get; set; }
        public int LastUsed { get; set; }

        public void Dispose()
        {
            if(Image!= null)
            {
                Image.Dispose();
                Image = null;
            }
        }
    }
}
