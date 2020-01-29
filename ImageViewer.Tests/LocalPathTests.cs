using System.IO;
using System.Reflection;

namespace ImageViewer.Tests
{
    public class LocalPathTests
    {
        protected string LocalPath(string fileName)
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);
        }
    }
}