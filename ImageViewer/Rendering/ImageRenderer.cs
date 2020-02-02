using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageViewer.Rendering
{
    public class ImageRenderer : IRenderer
    {
        public ImageMeta Image { get; set; }

        private Control _control;

        public ImageRenderer(Control control)
        {
            _control = control;
        }

        public void Render(Graphics g)
        {
            
        }
    }
}
