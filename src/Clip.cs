using System.Collections.Specialized;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShotTray
{
    static class Clip
    {
        // Image + file: Ctrl+V pastes the picture in chat/editors and the file in Explorer.
        public static void SetImageAndFile(Image image, string path)
        {
            var data = new DataObject();
            data.SetImage(image);
            data.SetFileDropList(new StringCollection { path });
            Set(data);
        }

        // Recordings: Ctrl+V pastes the video file in Explorer, chat apps and browsers.
        public static void SetFile(string path)
        {
            var data = new DataObject();
            data.SetFileDropList(new StringCollection { path });
            Set(data);
        }

        public static void SetImage(Image image)
        {
            var data = new DataObject();
            data.SetImage(image);
            Set(data);
        }

        public static void SetText(string text)
        {
            var data = new DataObject();
            data.SetText(text);
            Set(data);
        }

        static void Set(DataObject data)
        {
            // The clipboard can be briefly locked by another app; retry, then give up quietly.
            try { Clipboard.SetDataObject(data, true, 10, 100); }
            catch (ExternalException) { }
        }
    }
}
