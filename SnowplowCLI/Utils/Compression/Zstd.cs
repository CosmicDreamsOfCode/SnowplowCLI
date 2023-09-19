using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnowplowCLI.Utils.Compression
{
    public class Zstd
    {
        public static byte[] Decompress(byte[] b)
        {
            using (var decompressor = new ZstdNet.Decompressor())
            {
                return decompressor.Unwrap(b);
            }
        }
    }
}
