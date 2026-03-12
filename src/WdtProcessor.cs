using System.Buffers.Binary;
using System.Text;

namespace ii.PeasantCap;

public class WdtProcessor
{
	public byte[] Read(string filePath)
	{
		using var br = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
		return Read(br);
	}

	public byte[] Read(byte[] data)
	{
		using var ms = new MemoryStream(data);
		using var br = new BinaryReader(ms);
		return Read(br);
	}

	private static byte[] Read(BinaryReader br)
	{
		var stream = br.BaseStream;

		var signature = br.ReadBytes(4);
		if (signature[0] != 'L' || signature[1] != 'Z' || signature[2] != 'S' || signature[3] != 'S')
			throw new InvalidDataException($"Expected 'LZSS' magic, got '{Encoding.ASCII.GetString(signature)}'.");

		var totalSize = br.ReadUInt32();
		var chunkSize = br.ReadUInt32();
		var flags = br.ReadUInt16();
		var mode = (byte)(flags & 0xFF);

		if (mode != 0xC4)
			throw new NotSupportedException($"Unsupported LZSS mode 0x{mode:X2}");

		var chunkCount = (int)((totalSize + chunkSize - 1) / chunkSize);

		var chunkOffsets = new uint[chunkCount + 1];
		for (var i = 0; i < chunkCount; i++)
		{ 
			chunkOffsets[i] = br.ReadUInt32();
		}
		chunkOffsets[chunkCount] = (uint)stream.Length;

		var result = new byte[totalSize];
		var resultOffset = 0;

		for (var i = 0; i < chunkCount; i++)
		{
			var decompressedChunkSize = (i < chunkCount - 1) ? (int)chunkSize : (int)(totalSize - chunkSize * (uint)(chunkCount - 1));

			var chunkLength = (int)(chunkOffsets[i + 1] - chunkOffsets[i]);
			var buffer = new byte[chunkLength];

			stream.Position = chunkOffsets[i];
			stream.ReadExactly(buffer, 0, chunkLength);

			DecodeChunkC4(buffer, result, resultOffset, decompressedChunkSize);
			resultOffset += decompressedChunkSize;
		}

		return result;
	}

	// LZSS mode 0xC4: 12-bit window offset, 4-bit copy length, 4096-byte window.
	//
	// Each iteration reads 32 bits from src at srcPtr, byte-swaps to big-endian,
	// then shifts by bitPos (0-7) to align the current token at bit 31.
	//
	// Token type (bit 31 after shift):
	//   1 = LITERAL:        advance 1 byte, emit bits 30-23 as literal byte
	//   0 = BACK-REFERENCE: advance 2 bytes
	//                       bits 30-19 = 12-bit window offset
	//                       bits 18-15 = 4-bit length field
	//
	// Back-reference with offset == 0 emits (lengthBits+2) zero bytes.
	// Otherwise back_dist = 4096 - offset, copy (lengthBits>>1)+1 pairs,
	// plus 1 extra if lengthBits is odd (total 2..17 bytes).

	private static void DecodeChunkC4(byte[] source, byte[] output, int outputStart, int maxOutput)
	{
		var outputPosition = outputStart;
		var outputEnd = outputStart + maxOutput;
		var sourcePosition = 0;
		var bitState = 0;
		var sourceEnd = source.Length - 1;

		while (outputPosition < outputEnd && sourcePosition < sourceEnd)
		{
			var rawToken = ReadU32LE(source, sourcePosition);
			var bigEndianToken = BinaryPrimitives.ReverseEndianness(rawToken);

			var bitOffset = bitState & 0x07;
			var alignedToken = bigEndianToken << bitOffset;
			var shiftedPayload = alignedToken << 1;

			var sourceAdvance = bitState > 0xDFFE ? 1 : 0;
			bitState = (bitState + 0x2001) & 0xFF07;

			if ((alignedToken & 0x80000000u) != 0)
			{
				sourcePosition += sourceAdvance + 1;
				output[outputPosition++] = (byte)(shiftedPayload >> 24);
			}
			else
			{
				sourcePosition += sourceAdvance + 2;

				var windowOffset = (int)((shiftedPayload >> 20) & 0xFFF);
				var lengthField = (int)((shiftedPayload >> 16) & 0x0F);

				if (windowOffset == 0)
				{
					var zeroRunLength = lengthField + 2;
					for (var i = 0; i < zeroRunLength && outputPosition < outputEnd; i++)
						output[outputPosition++] = 0;
				}
				else
				{
					var backReferenceDistance = 0x1000 - windowOffset;

					if ((lengthField & 1) != 0 && outputPosition < outputEnd)
					{
						output[outputPosition] = (outputPosition - outputStart) >= backReferenceDistance ? output[outputPosition - backReferenceDistance] : (byte)0;
						outputPosition++;
					}

					var pairCount = (lengthField >> 1) + 1;
					for (var i = 0; i < pairCount; i++)
					{
						if (outputPosition >= outputEnd)
							break;
						output[outputPosition] = (outputPosition - outputStart) >= backReferenceDistance ? output[outputPosition - backReferenceDistance] : (byte)0;
						outputPosition++;

						if (outputPosition >= outputEnd)
							break;
						output[outputPosition] = (outputPosition - outputStart) >= backReferenceDistance ? output[outputPosition - backReferenceDistance] : (byte)0;
						outputPosition++;
					}
				}
			}
		}
	}

	private static uint ReadU32LE(byte[] data, int pos)
	{
		if (pos >= data.Length)
			return 0;

		if (pos + 4 <= data.Length)
			return BitConverter.ToUInt32(data, pos);

		Span<byte> buf = stackalloc byte[4];
		data.AsSpan(pos).CopyTo(buf);
		return BitConverter.ToUInt32(buf);
	}
}