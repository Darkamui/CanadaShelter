namespace Shelter.ArchitectureTests.Rules;

internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    // The repository root is the first ancestor holding global.json.
    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "global.json")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (global.json) not found.");
    }
}
