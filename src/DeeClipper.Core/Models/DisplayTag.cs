namespace DeeClipper.Core.Models;

public static class DisplayTag
{
    public const string Email = "Email";
    public const string Phone = "Phone";
    public const string Color = "Color";
    public const string Code = "Code";
    public const string Number = "Number";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Email,
        Phone,
        Color,
        Code,
        Number
    };
}
