namespace InterviewAPI.Helpers;

/// <summary>
/// Converts embedding vectors between float[] (ai-service JSON) and byte[]
/// (VARBINARY(MAX) storage). Little-endian float32, 4 bytes per element.
/// </summary>
public static class EmbeddingCodec
{
    public static byte[] ToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[] ToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, floats.Length * sizeof(float));
        return floats;
    }
}
