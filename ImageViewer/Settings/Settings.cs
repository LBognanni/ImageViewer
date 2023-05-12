namespace ImageViewer;

public class Settings
{
    public bool StartInFullScreen { get; set; }
    public decimal DefaultZoom { get; set; }
    public bool TransparentWhenOutOfFocus { get; set; }
    
    public Settings()
    {
        DefaultZoom = 0;
        StartInFullScreen = false;
        TransparentWhenOutOfFocus = true;
    }
}