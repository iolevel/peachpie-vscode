using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    public static class PathUtils
    {
        public static string NormalizePath(string path)
        {
            if (path.StartsWith("file:///"))
            {
                var uri = new Uri(path);
                path = Uri.UnescapeDataString(uri.AbsolutePath);

                // Fix /c:/...
                if (path.Length >= 3 && path[0] == '/' && path[2] == ':')
                {
                    path = path.Substring(1);
                }
            }

            path = path.Replace('\\', '/');

            if (path.EndsWith("/"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }
    }
}
