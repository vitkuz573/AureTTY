namespace AureTTY.Execution.Abstractions;

public sealed class ProcessCredentialOptions(string userName, string? password = null)
{
    public string UserName { get; } = userName;

    public string? Domain { get; init; }

    public string? Password { get; } = password;

    public bool LoadUserProfile { get; init; } = true;
}
