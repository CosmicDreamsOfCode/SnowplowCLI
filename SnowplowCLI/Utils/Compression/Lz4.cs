using K4os.Compression.LZ4.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnowplowCLI.Utils.Compression
{
    public class Lz4
    {
        public static byte[] Decompress(byte[] i)
        {
            using (MemoryStream ms = new MemoryStream(i))
            {
                using (var source = LZ4Stream.Decode(ms))
                    source.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
