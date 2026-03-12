using System.Text;

namespace ii.PeasantCap;

/*
Header
0x00	16	Signature	"HMMSYS PackFile\n" (with \n)
0x10	4	Version		Always 0x1A
0x14	12	Padding     Always 0
0x20	4	FileCount	Number of files in archive
0x24	4	FileOffset  Size of the entry table in bytes* 
 
Directory Listing
0x00   2 Path + filename length
0x02   varies Path + filename text
       4 FileOffset
       4 FileLength
       1 Unknown

File Content
Raw bytes
*/

public class HmmProcessor
{
	public List<(string filename, byte[] content)> Read(string filePath)
	{
		using var br = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
		return Read(br);
	}

	public List<(string filename, byte[] content)> Read(byte[] data)
	{
		using var ms = new MemoryStream(data);
		using var br = new BinaryReader(ms);
		return Read(br);
	}

	private static List<(string filename, byte[] content)> Read(BinaryReader br)
	{
		var stream = br.BaseStream;

		var expectedSignature = "HMMSYS PackFile\n"u8.ToArray();
		var signature = br.ReadBytes(expectedSignature.Length);
		if (signature.Length != expectedSignature.Length || !signature.AsSpan().SequenceEqual(expectedSignature))
			throw new InvalidDataException($"Expected 'HMMSYS PackFile\\n' magic, got '{Encoding.ASCII.GetString(signature)}'.");

		var version = br.ReadUInt32();
		if (version != 0x1A)
			throw new NotSupportedException($"Unsupported HMMSYS version 0x{version:X8}.");

		var padding = br.ReadBytes(12);
		if (padding.Length != 12 || padding.Any(static b => b != 0))
			throw new InvalidDataException("Invalid HMMSYS header padding. Expected 12 zero bytes.");

		var fileCount = br.ReadInt32();
		if (fileCount < 0)
			throw new InvalidDataException($"Invalid HMMSYS file count {fileCount}.");

		var directoryTableSize = br.ReadInt32();
		if (directoryTableSize < 0)
			throw new InvalidDataException($"Invalid HMMSYS directory table size {directoryTableSize}.");

		var directoryTableStart = stream.Position;
		var directoryTableEnd = directoryTableStart + directoryTableSize;
		if (directoryTableEnd > stream.Length)
			throw new InvalidDataException("HMMSYS directory table extends past end of stream.");

		var fileContentStart = directoryTableEnd;
		var entries = new List<(string filename, int fileOffset, int fileLength)>(fileCount);
		for (var i = 0; i < fileCount; i++)
		{
			if (stream.Position + 2 > directoryTableEnd)
				throw new InvalidDataException($"Unexpected end of HMMSYS directory table while reading entry {i}.");

			var nameLength = br.ReadUInt16();
			if (stream.Position + nameLength + 9 > directoryTableEnd)
				throw new InvalidDataException($"Directory entry {i} exceeds HMMSYS directory table size.");

			var nameBytes = br.ReadBytes(nameLength);
			var filename = Encoding.UTF8.GetString(nameBytes);
			var fileOffset = br.ReadInt32();
			var fileLength = br.ReadInt32();
			_ = br.ReadByte();

			if (fileOffset < 0 || fileLength < 0)
				throw new InvalidDataException($"Directory entry {i} has negative offset/length.");

			entries.Add((filename, fileOffset, fileLength));
		}

		var result = new List<(string filename, byte[] content)>(fileCount);
		foreach (var (filename, fileOffset, fileLength) in entries)
		{
			long absoluteOffset;
			var relativeOffset = fileContentStart + fileOffset;
			var relativeValid = relativeOffset >= 0 && relativeOffset + fileLength <= stream.Length;
			var absoluteValid = fileOffset >= 0 && (long)fileOffset + fileLength <= stream.Length;

			if (relativeValid)
				absoluteOffset = relativeOffset;
			else if (absoluteValid)
				absoluteOffset = fileOffset;
			else
				throw new InvalidDataException($"File '{filename}' points outside HMMSYS stream.");

			var content = new byte[fileLength];
			stream.Position = absoluteOffset;
			stream.ReadExactly(content, 0, fileLength);
			result.Add((filename, content));
		}

		return result;
	}
}