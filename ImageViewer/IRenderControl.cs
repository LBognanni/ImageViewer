using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageViewer
{
    public interface IRenderControl
    {
        Point Pan { get; }
        int Rotation { get; }
        int Width { get; }
        int Height { get; }
        decimal Zoom { get; }
    }
}
