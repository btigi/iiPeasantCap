iiPeasantCap
=========

iiPeasantCap is a C# library supporting the modification of files relating to Tzar: The Burden of the Crown, the 2000 RTS game developed by Haemimont Games.

| Name   | Read | Write | Comment
|--------|:----:|-------|--------
| AI     | ✗   |   ✗   | Plain text
| AIS    | ✗   |   ✗   | Plain text
| BAK    | ✗   |   ✗   | Plain text
| BMP    | ✗   |   ✗   | Standard bitmap
| DAT    | ✗   |   ✗   | 
| DDV    | ✗   |   ✗   | Video
| FNT    | ✗   |   ✗   | Font
| HMM    | ✔   |   ✗   | Archive
| INI    | ✗   |   ✗   | Plain text
| MID    | ✗   |   ✗   | Music
| PAL    | ✗   |   ✗   | Palette
| RLE    | ✔   |   ✗   | Graphics
| TXT    | ✗   |   ✗   | Plain text
| WAV    | ✗   |   ✗   | Sounds
| WCM    | ✗   |   ✗   | Campaign
| WDT    | ✔   |   ✗   | Archive
| WDM    | ✗   |   ✗   | Demo
| WMP    | ✗   |   ✗   | Map
| WSV    | ✗   |   ✗   | Save file


## Usage

```csharp
var wdtProcessor = new WdtProcessor();
var bytes = wdtProcessor.Read(@"X:\source\tzar\data.wdt");
File.WriteAllBytes(@"X:\source\tzar\data.hmm", bytes);



var rleProcessor = new RleProcessor();

var eo = new EnumerationOptions();
eo.RecurseSubdirectories = true;
foreach (var f in Directory.EnumerateFiles(@"D:\Games\Tzar", "*.rle", eo))
{
	var rle = rleProcessor.Read(f);
	for (int fi = 0; fi < rle.Frames.Count; fi++)
	{
		var frame = rle.Frames[fi];
		string savedAs = "-";

		savedAs = Path.Combine("X:\\source\\tests\\iiPeasantCapTest\\rle-output", $"{Path.GetFileNameWithoutExtension(f)}_frame{fi:D3}.png");
		frame.Image.SaveAsPng(savedAs);
	}
}
```

## Compiling

To clone and run this application, you'll need [Git](https://git-scm.com) and [.NET](https://dotnet.microsoft.com/) installed on your computer. From your command line:

```
# Clone this repository
$ git clone https://github.com/btigi/iiPeasantCap

# Go into the repository
$ cd src

# Build  the app
$ dotnet build
```

## Licencing

iiPeasantCap is licenced under the MIT License. Full licence details are available in licence.md

LZSS decompression based on work by [https://dganev.com/](https://dganev.com/posts/2025-11-28-re-tzar/)