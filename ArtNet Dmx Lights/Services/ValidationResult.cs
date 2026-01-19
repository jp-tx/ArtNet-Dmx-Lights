namespace ArtNet_Dmx_Lights.Services;

public sealed class ValidationResult
{
    public List<string> Errors { get; } = [];
    public bool IsValid => Errors.Count == 0;
}
