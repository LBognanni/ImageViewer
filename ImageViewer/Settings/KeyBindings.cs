using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ImageViewer;

public class KeyBinding
{
    public string? Action { get; set; }
    public bool IsDeleteAction { get; set; }
    public string? Command { get; set; }

    public KeyBinding()
    {
    }

    public KeyBinding(string action)
    {
        Action = action;
    }
}
