using System;
using System.IO;

namespace XLinkLibrary.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var file = new XLink(args[0], XLink.UserStructure.ELinkBOTW);
            string folder = Path.GetFileNameWithoutExtension(args[0]);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            foreach (var header in file.Entries)
            {
                header.Export($"{folder}\\{header.Name}.json");
            }
        }
    }
}
