using System;
using System.Collections.Generic;
using System.IO;

namespace Hallonkriget.Sim.Commands;

/// <summary>
/// Gör kommandon till bytes och tillbaka. Samma format används i nätverket och i reprisfiler.
/// Heltalen skrivs som zigzag-varint, så ett vanligt kommando blir 5–10 byte.
/// </summary>
public static class CommandCodec
{
    public static void Write(Stream stream, in Command command)
    {
        WriteVarInt(stream, command.Tick);
        stream.WriteByte(command.Player);
        stream.WriteByte((byte)command.Type);
        WriteVarInt(stream, command.A);
        WriteVarInt(stream, command.B);
        WriteVarInt(stream, command.C);
    }

    public static Command Read(Stream stream)
    {
        int tick = ReadVarInt(stream);
        byte player = ReadByte(stream);
        var type = (CommandType)ReadByte(stream);
        int a = ReadVarInt(stream);
        int b = ReadVarInt(stream);
        int c = ReadVarInt(stream);
        return new Command(tick, player, type, a, b, c);
    }

    public static byte[] Encode(IReadOnlyList<Command> commands)
    {
        using var ms = new MemoryStream();
        WriteVarInt(ms, commands.Count);
        for (int i = 0; i < commands.Count; i++) Write(ms, commands[i]);
        return ms.ToArray();
    }

    public static List<Command> Decode(byte[] data)
    {
        using var ms = new MemoryStream(data, writable: false);
        int count = ReadVarInt(ms);
        var list = new List<Command>(count);
        for (int i = 0; i < count; i++) list.Add(Read(ms));
        return list;
    }

    internal static void WriteVarInt(Stream stream, int value)
    {
        uint zigzag = (uint)((value << 1) ^ (value >> 31));
        while (zigzag >= 0x80)
        {
            stream.WriteByte((byte)(zigzag | 0x80));
            zigzag >>= 7;
        }
        stream.WriteByte((byte)zigzag);
    }

    internal static int ReadVarInt(Stream stream)
    {
        uint result = 0;
        int shift = 0;
        while (true)
        {
            byte b = ReadByte(stream);
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 28) throw new InvalidDataException("För lång varint");
        }
        return (int)(result >> 1) ^ -(int)(result & 1);
    }

    internal static byte ReadByte(Stream stream)
    {
        int b = stream.ReadByte();
        if (b < 0) throw new EndOfStreamException();
        return (byte)b;
    }
}
