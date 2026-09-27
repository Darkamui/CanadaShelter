namespace Shelter.Testing;

public static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string RolesInitScript => Path.Combine(Root, "infrastructure", "docker", "postgres", "init", "01-roles.sh");

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
