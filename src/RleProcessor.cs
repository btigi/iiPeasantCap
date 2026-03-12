using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ii.PeasantCap.Model.Rle;

namespace ii.PeasantCap;

public class RleProcessor
{
	private const int HeaderSize = 38;

	public RleFile Read(string filePath)
	{
		using var br = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
		return Read(br);
	}

	public RleFile Read(byte[] data)
	{
		using var ms = new MemoryStream(data);
		using var br = new BinaryReader(ms);
		return Read(br);
	}

	private RleFile Read(BinaryReader br)
	{
		var stream = br.BaseStream;

		if (stream.Length < HeaderSize)
			throw new InvalidDataException($"File too small for RLE header ({stream.Length} < {HeaderSize} bytes).");

		stream.Position = 0;
		var header = ReadHeader(br);

		if (header.Width == 0 || header.Height == 0)
			throw new InvalidDataException($"Implausible dimensions {header.Width}x{header.Height}.");

		long payloadStart = stream.Position;
		long payloadLength = stream.Length - payloadStart;

		var frameOffsets = FindFrameStarts(br, stream, payloadStart, payloadLength, header.Height);

		if (frameOffsets.Count == 0)
			throw new InvalidDataException("No valid frame tables found in RLE payload.");

		var frames = new List<RleFrame>(frameOffsets.Count);
		foreach (var offset in frameOffsets)
		{
			var image = DecodeFrame(br, stream, payloadStart, offset, header.Width, header.Height);
			frames.Add(new RleFrame { Image = image, PayloadOffset = offset });
		}

		return new RleFile { Header = header, Frames = frames };
	}

	private static RleHeader ReadHeader(BinaryReader br)
	{
		return new RleHeader
		{
			Signature = br.ReadUInt32(),
			VersionA = br.ReadUInt32(),
			VersionB = br.ReadUInt32(),
			Reserved0 = br.ReadUInt32(),
			Tag = Encoding.ASCII.GetString(br.ReadBytes(4)),
			Width = br.ReadUInt16(),
			Height = br.ReadUInt16(),
			ImageCountHint = br.ReadUInt16(),
			PayloadSize = br.ReadUInt32(),
			Reserved1 = br.ReadUInt32(),
			Unknown34 = br.ReadUInt32(),
		};
	}

	// A valid frame start has a row-offset table of `height` strictly-increasing
	// uint32 values where entry[0] == height * 4 (table size) and all values
	// fit within the remaining payload from that position.

	private static List<long> FindFrameStarts(
		BinaryReader br, Stream stream,
		long payloadStart, long payloadLength, ushort height)
	{
		var starts = new List<long>();
		long tableBytes = (long)height * 4;

		if (payloadLength < tableBytes)
			return starts;

		long scanEnd = payloadLength - tableBytes;

		for (long off = 0; off <= scanEnd; off += 4)
		{
			if (IsValidFrameAt(br, stream, payloadStart + off, payloadStart + payloadLength, height, tableBytes))
				starts.Add(off);
		}

		return starts;
	}

	private static bool IsValidFrameAt(BinaryReader br, Stream stream, long absPos, long absEnd, ushort height, long tableBytes)
	{
		long remaining = absEnd - absPos;
		if (remaining < tableBytes)
			return false;

		stream.Position = absPos;
		uint prev = 0;

		for (var i = 0; i < height; i++)
		{
			uint val = br.ReadUInt32();
			if (i == 0)
			{
				if (val != (uint)tableBytes)
					return false;
			}
			else
			{
				if (val <= prev)
					return false;
			}
			if (val > (uint)remaining)
				return false;
			prev = val;
		}

		return true;
	}

	// Row-span format (per row):
	//   repeat: (skip: byte, count: byte, pixels: count × uint16-LE)
	//   end:    count == 0
	// Pixel format: X1R5G5B5 (RGB555)

	private static Image<Rgba32> DecodeFrame(BinaryReader br, Stream stream, long payloadStart, long framePayloadOffset, ushort width, ushort height)
	{
		long tableAbsPos = payloadStart + framePayloadOffset;

		stream.Position = tableAbsPos;
		var rowOffsets = new uint[height];
		for (var i = 0; i < height; i++)
			rowOffsets[i] = br.ReadUInt32();

		var image = new Image<Rgba32>(width, height);

		for (var y = 0; y < height; y++)
		{
			long rowAbsStart = tableAbsPos + rowOffsets[y];
			long rowAbsEnd = (y + 1 < height) ? tableAbsPos + rowOffsets[y + 1] : stream.Length;

			stream.Position = rowAbsStart;
			var x = 0;

			while (stream.Position < rowAbsEnd && x <= width)
			{
				if (stream.Position + 2 > rowAbsEnd)
					break;

				int skip = stream.ReadByte();
				int count = stream.ReadByte();

				if (count == 0)
					break;

				x += skip;

				for (var p = 0; p < count; p++)
				{
					if (stream.Position + 2 > rowAbsEnd)
						break;
					ushort pixel = br.ReadUInt16();
					if (x < width)
						image[x, y] = Rgb555ToRgba32(pixel);
					x++;
				}
			}
		}

		return image;
	}

	private static Rgba32 Rgb555ToRgba32(ushort p)
	{
		byte r = (byte)((p >> 10) & 0x1F);
		byte g = (byte)((p >> 5) & 0x1F);
		byte b = (byte)(p & 0x1F);

		return new Rgba32(
			(byte)(r * 255 / 31),
			(byte)(g * 255 / 31),
			(byte)(b * 255 / 31),
			255);
	}
}