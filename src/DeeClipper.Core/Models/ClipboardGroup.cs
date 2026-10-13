namespace DeeClipper.Core.Models;

public class ClipboardGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#0D9488";
    public int SortOrder { get; set; }
}
