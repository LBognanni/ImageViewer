using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageViewer.Rendering
{
    public interface IRenderer
    {
        void Render(Graphics g);
    }
}
