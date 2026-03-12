using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ii.PeasantCap.Model.Rle;

public class RleFrame
{
    public required Image<Rgba32> Image { get; set; }
    public long PayloadOffset { get; set; }
}