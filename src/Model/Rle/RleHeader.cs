namespace ii.PeasantCap.Model.Rle;

public class RleHeader
{
    public uint Signature { get; set; }
    public uint VersionA { get; set; }
    public uint VersionB { get; set; }
    public uint Reserved0 { get; set; }
    public string Tag { get; set; } = string.Empty;
    public ushort Width { get; set; }
    public ushort Height { get; set; }
    public ushort ImageCountHint { get; set; }
    public uint PayloadSize { get; set; }
    public uint Reserved1 { get; set; }
    public uint Unknown34 { get; set; }
}