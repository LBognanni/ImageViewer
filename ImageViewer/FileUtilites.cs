namespace ImageViewer;

public class FileUtilites
{
    public static (string exe, string parameters) SplitCommand(string command)
    {
        string exe, parameters;
        int start, length;
        if (command.StartsWith("\""))
        {
            start = 1;
            length = command.IndexOf('"', 1) - start;
        }
        else
        {
            start = 0;
            length = command.IndexOf(' ');
        }

        if (length > 0)
        {
            exe = command.Substring(start, length);
            parameters = command.Substring(start + length + 1);
        }
        else
        {
            exe = command;
            parameters = "";
        }

        return (exe.Trim(), parameters.Trim());
    }
}