namespace Surab.Compilation;

internal class Emitter
{
    private readonly List<byte> _chunk = new();

    public Emitter()
    {
    }

    public byte[] Chunk => _chunk.ToArray();

    public void Emit(int v)
    {
        Emit((byte)(v & 0xff));
        Emit((byte)((v >> 8) & 0xff));
        Emit((byte)((v >> 16) & 0xff));
        Emit((byte)((v >> 24) & 0xff));
    }

    public void Emit(OpCode op, int v1)
    {
        Emit(op);
        Emit(v1);
    }

    public void Emit(OpCode op) => Emit((ushort)op);

    public void Emit(ushort v)
    {
        Emit((byte)(v & 0xff));
        Emit((byte)((v >> 8) & 0xff));
    }

    public void Emit(byte v)
    {
        _chunk.Add(v);
    }
}
