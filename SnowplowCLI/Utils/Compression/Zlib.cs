using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnowplowCLI.Utils.Compression
{
    public class Zlib
    {
        public static byte[] Decompress(byte[] i)
        {
            MemoryStream ms = new MemoryStream(i);
            using (DataStream stream = new DataStream(ms))
            {
                stream.Position = 2;
                using (var ds = new DeflateStream(new MemoryStream(stream.ReadBytes((int)stream.Length - 6)), CompressionMode.Decompress))
                    ds.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
