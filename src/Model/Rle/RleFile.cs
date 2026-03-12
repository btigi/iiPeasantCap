namespace ii.PeasantCap.Model.Rle;

public class RleFile
{
    public required RleHeader Header { get; set; }
    public List<RleFrame> Frames { get; set; } = [];
}