using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot_Jane.Core.Utils
{
    public static class CompressionUtils
    {
        public static void Compress(string filePath, string directoryPath)
        {
            // Remove any file seperators from end of path since FileInfo doesn't like those.
            if (filePath.EndsWith(Path.DirectorySeparatorChar.ToString())) filePath = filePath.Truncate(filePath.Length - 1);
            var fileToCompress = new FileInfo(filePath);
            using (FileStream originalFileStream = fileToCompress.OpenRead())
            {
                if ((File.GetAttributes(fileToCompress.FullName) &
                     FileAttributes.Hidden) != FileAttributes.Hidden & fileToCompress.Extension != ".gz")
                {
                    using (FileStream compressedFileStream = File.Create(fileToCompress.FullName + ".gz"))
                    {
                        using (GZipStream compressionStream = new GZipStream(compressedFileStream,
                            CompressionMode.Compress))
                        {
                            originalFileStream.CopyTo(compressionStream);

                        }
                    }
                    FileInfo info = new FileInfo(directoryPath + "\\" + fileToCompress.Name + ".gz");
                    Console.WriteLine("Compressed {0} from {1} to {2} bytes.",
                        fileToCompress.Name, fileToCompress.Length.ToString(), info.Length.ToString());
                }

            }
        }
    }
}
