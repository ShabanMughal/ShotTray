using System;
using System.IO;
using System.Linq;

namespace ShotTray
{
    // Where screenshots and recordings live. Shared by the tray app and the `shot` CLI.
    static class Store
    {
        public const int KeepDays = 7;

        public static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShotTray", "Screenshots");

        public static string NewPath()
        {
            return NewPath("shot_", ".png");
        }

        public static string NewVideoPath()
        {
            return NewPath("rec_", ".mp4");
        }

        static string NewPath(string prefix, string ext)
        {
            Directory.CreateDirectory(Folder);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = Path.Combine(Folder, prefix + stamp + ext);
            for (int n = 2; File.Exists(path); n++)
                path = Path.Combine(Folder, prefix + stamp + "_" + n + ext);
            return path;
        }

        // Newest first.
        public static FileInfo[] Recent()
        {
            if (!Directory.Exists(Folder)) return new FileInfo[0];
            return new DirectoryInfo(Folder).GetFiles("shot_*.png")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToArray();
        }

        public static void Cleanup()
        {
            if (!Directory.Exists(Folder)) return;
            DateTime cutoff = DateTime.Now.AddDays(-KeepDays);
            var dir = new DirectoryInfo(Folder);
            foreach (FileInfo f in dir.GetFiles("shot_*.png").Concat(dir.GetFiles("rec_*.mp4")))
            {
                if (f.LastWriteTime >= cutoff) continue;
                try { f.Delete(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
