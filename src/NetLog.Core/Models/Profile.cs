namespace NetLog.Core.Models;

/// <summary>User convenience state. Recent <b>root</b> folders (each holds run subfolders) the user
/// has opened, most-recent first.</summary>
public sealed record Profile(IReadOnlyList<string> RecentRootPaths);
