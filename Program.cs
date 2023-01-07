using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nltx_tool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) return;
            string ext = Path.GetExtension(args[0]);
            NLTX nltx = new NLTX();
            if(ext == ".nltx")
            {
                nltx.Extract(args[0]);
                return;
            }
            else if (ext == ".png")
            {
                nltx.Build(args[0]);
                return;
            }
        }
    }
}
