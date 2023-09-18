using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using SnowplowCLI.Utils;

namespace SnowplowCLI.Utils;

public class BlockStream : DataStream
{
    private readonly Block<byte> m_block;

    public BlockStream(int inSize)
    {
        m_block = new Block<byte>(inSize);
        m_stream = m_block.ToStream();
    }

    public BlockStream(Block<byte> inBuffer)
    {
        m_block = inBuffer;
        m_stream = m_block.ToStream();
    }

    /// <summary>
    /// Loads whole file into memory.
    /// </summary>
    /// <param name="inPath">The path of the file</param>
    /// <returns>A <see cref="BlockStream"/> that has the file loaded.</returns>
    public static BlockStream FromFile(string inPath)
    {
        using (FileStream stream = new(inPath, FileMode.Open, FileAccess.Read))
        {
            BlockStream retVal;
            retVal = new BlockStream((int)stream.Length);
            stream.ReadExactly(retVal.m_block);
            return retVal;
        }
    }

    /// <summary>
    /// Loads part of a file into memory.
    /// </summary>
    /// <param name="inPath">The path of the file.</param>
    /// <param name="inOffset">The offset of the data to load.</param>
    /// <param name="inSize">The size of the data to load</param>
    /// <returns>A <see cref="BlockStream"/> that has the data loaded.</returns>
    public static BlockStream FromFile(string inPath, long inOffset, int inSize)
    {
        using (FileStream stream = new(inPath, FileMode.Open, FileAccess.Read))
        {
            stream.Position = inOffset;

            BlockStream retVal = new(inSize);

            stream.ReadExactly(retVal.m_block);
            return retVal;
        }
    }

    /// <summary>
    /// <see cref="Aes"/> decrypt this <see cref="BlockStream"/>.
    /// </summary>
    /// <param name="inKey">The key to use for the decryption.</param>
    /// <param name="inPaddingMode">The <see cref="PaddingMode"/> to use for the decryption.</param>
    public void Decrypt(byte[] inKey, PaddingMode inPaddingMode)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = inKey;
            Span<byte> span = m_block.ToSpan((int)Position);
            aes.DecryptCbc(span, inKey, span, inPaddingMode);
        }
    }

    /// <summary>
    /// <see cref="Aes"/> decrypt part of this <see cref="BlockStream"/>.
    /// </summary>
    /// <param name="inKey">The key to use for the decryption.</param>
    /// <param name="inSize">The size of the data to decrypt.</param>
    /// <param name="inPaddingMode">The <see cref="PaddingMode"/> to use for the decryption.</param>
    public void Decrypt(byte[] inKey, int inSize, PaddingMode inPaddingMode)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = inKey;
            Span<byte> span = m_block.ToSpan((int)Position, inSize);
            aes.DecryptCbc(span, inKey, span, inPaddingMode);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        m_block.Dispose();
        GC.SuppressFinalize(this);
    }

}