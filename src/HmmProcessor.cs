using System.Text;

namespace ii.PeasantCap;

/*
Header
0x00  16  Signature         "HMMSYS PackFile\n"
0x10   4  Version           Always 0x1A
0x14  12  Padding           Always 0
0x20   4  FileCount         Number of files in archive
0x24   4  EntryTableSize    Size of the entry table in bytes

Directory Listing
- Entry 0
    2    nameLength
    var  name
    4    fileOffset
    4    fileLength
- Entry n
    1    reconstructedNameLength
    1    prefixCopyLength
    var   storedName bytes
    4    fileOffset
    4    fileLength
  storedNameLen = reconstructedNameLength - prefixCopyLength
  fullName = previousFullName[0..prefixCopyLength] + storedName

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
		if (directoryTableStart < 0 || directoryTableEnd < directoryTableStart || directoryTableEnd > stream.Length)
			throw new InvalidDataException("HMMSYS directory table extends past end of stream.");

		var entries = new List<(string filename, int fileOffset, int fileLength)>(fileCount);
		string? previousFilename = null;

		for (var i = 0; i < fileCount; i++)
		{
			if (i == 0)
			{
				if (stream.Position + 2 > directoryTableEnd)
					throw new InvalidDataException("Unexpected end of Directory entry table while reading first entry.");

				var nameLength = br.ReadUInt16();
				if (stream.Position + nameLength + 8 > directoryTableEnd)
					throw new InvalidDataException("First Directory entry exceeds Directory table bounds.");

				var nameBytes = br.ReadBytes(nameLength);
				var firstFilename = Encoding.Latin1.GetString(nameBytes);
				var firstFileOffset = br.ReadInt32();
				var firstFileLength = br.ReadInt32();

				if (firstFileOffset < 0 || firstFileLength < 0)
					throw new InvalidDataException("First Directory entry has negative offset/length.");

				entries.Add((firstFilename, firstFileOffset, firstFileLength));
				previousFilename = firstFilename;
				continue;
			}

			if (stream.Position + 2 > directoryTableEnd)
				throw new InvalidDataException($"Unexpected end of Directory entry table while reading entry {i}.");

			var reconstructedNameLength = br.ReadByte();
			var prefixCopyLength = br.ReadByte();
			var storedNameLength = reconstructedNameLength - prefixCopyLength;
			if (storedNameLength < 0)
				throw new InvalidDataException($"Invalid directory prefix data in entry {i}: reconstructedNameLength={reconstructedNameLength}, prefixCopyLength={prefixCopyLength}.");

			if (stream.Position + storedNameLength + 8 > directoryTableEnd)
				throw new InvalidDataException($"Directory entry {i} exceeds entry table bounds.");

			var storedNameBytes = br.ReadBytes(storedNameLength);
			var storedName = Encoding.Latin1.GetString(storedNameBytes);
			var fileOffset = br.ReadInt32();
			var fileLength = br.ReadInt32();

			if (fileOffset < 0 || fileLength < 0)
				throw new InvalidDataException($"Directory entry {i} has negative offset/length.");

			if (previousFilename is null || prefixCopyLength > previousFilename.Length)
				throw new InvalidDataException($"Directory entry {i} has invalid prefix copy length {prefixCopyLength}.");

			var filename = string.Concat(previousFilename.AsSpan(0, prefixCopyLength), storedName);
			entries.Add((filename, fileOffset, fileLength));
			previousFilename = filename;
		}

		if (stream.Position > directoryTableEnd)
			throw new InvalidDataException("Directory entry parsing progressed outside entry table.");

		var result = new List<(string filename, byte[] content)>(fileCount);
		foreach (var (filename, fileOffset, fileLength) in entries)
		{
			var fileEnd = (long)fileOffset + fileLength;
			if (fileEnd > stream.Length)
				throw new InvalidDataException($"File '{filename}' points outside HMMSYS stream.");

			var content = new byte[fileLength];
			stream.Position = fileOffset;
			stream.ReadExactly(content, 0, fileLength);
			result.Add((filename, content));
		}

		return result;
	}
}