using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using AureTTY.Execution.Abstractions;

namespace AureTTY.Linux.Services;

public sealed class CommandLineProvider(IFileSystem fileSystem) : ICommandLineProvider
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public async Task<string[]> GetCommandLineAsync(IProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (process.HasExited)
        {
            return [];
        }

        try
        {
            var processId = process.Id.ToString(CultureInfo.InvariantCulture);
            var commandLinePath = _fileSystem.Path.Combine("/proc", processId, "cmdline");

            if (!_fileSystem.File.Exists(commandLinePath))
            {
                return [];
            }

            await using var stream = _fileSystem.FileStream.New(
                commandLinePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            if (memory.Length == 0)
            {
                return [];
            }

            var payload = Encoding.UTF8.GetString(memory.ToArray());
            return [.. payload.Split('\0', StringSplitOptions.RemoveEmptyEntries)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [];
        }
    }
}
