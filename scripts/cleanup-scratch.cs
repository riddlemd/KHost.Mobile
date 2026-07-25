#!/usr/bin/env dotnet
// cleanup-scratch.cs — reclaim disk from the two gitignored scratch folders that grow without bound:
// device-backups/ (a ~48 MB tarball per run of backup-device-data.cs) and playwright/shots/ (a PNG per
// screenshot the UI walkers take). A day of on-device work leaves well over a gigabyte in the two.
//
// Portable by design, same as backup-device-data.cs: a .NET 10 *file-based app*, so it runs identically on
// Windows, Linux and macOS with only the SDK every contributor already has. No shell, no find(1).
//
// WHY A SCRIPT AND NOT `rm`: backups are the ONLY safety net against a redeploy that reinstalls (see
// DEVELOPMENT.md → Backing up on-device data). Deleting the wrong ones is unrecoverable, so this keeps the
// newest N by default, never deletes the newest one without --keep 0, and refuses to touch anything it
// didn't recognise as its own artifact.
//
// Usage:
//   dotnet run scripts/cleanup-scratch.cs --                    # keep the 3 newest backups, clear all shots
//   dotnet run scripts/cleanup-scratch.cs -- --dry-run          # show what would go, delete nothing
//   dotnet run scripts/cleanup-scratch.cs -- --keep 5           # keep the 5 newest backups
//   dotnet run scripts/cleanup-scratch.cs -- --backups-only     # leave shots/ alone
//   dotnet run scripts/cleanup-scratch.cs -- --shots-only       # leave device-backups/ alone
//   (On Unix you can also `chmod +x` this file and run ./scripts/cleanup-scratch.cs)
//
// Options:
//   -n, --dry-run        print what would be deleted, then exit without deleting
//   -k, --keep <n>       how many of the newest backups to keep (default 3; 0 empties the folder)
//       --backups-only   only clean device-backups/
//       --shots-only     only clean playwright/shots/
//   -h, --help           this text

using System.Runtime.CompilerServices;

const int DefaultKeep = 3;

// Only files matching these are ever considered — anything else in the folders is left alone, so a note or a
// hand-saved archive parked there survives.
const string BackupPattern = "khost-mobile-*.tar.gz";
string[] shotExtensions = [".png", ".jpg", ".jpeg", ".webp"];

// --- resolve paths relative to THIS script's location, so it works regardless of the current directory ---
string scriptDir = Path.GetDirectoryName(ScriptLocation.File()) is { Length: > 0 } d && Directory.Exists(d)
    ? d
    : Directory.GetCurrentDirectory();
string repoRoot = Path.GetDirectoryName(scriptDir) ?? scriptDir;
string backupDir = Path.Combine(repoRoot, "device-backups");
string shotsDir = Path.Combine(repoRoot, "playwright", "shots");

int keep = DefaultKeep;
bool dryRun = false, doBackups = true, doShots = true;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-n" or "--dry-run":
            dryRun = true;
            break;
        case "-k" or "--keep":
            if (i + 1 >= args.Length) Die("--keep needs a value.");
            if (!int.TryParse(args[++i], out keep) || keep < 0) Die("--keep needs a whole number (0 or more).");
            break;
        case "--backups-only":
            doShots = false;
            break;
        case "--shots-only":
            doBackups = false;
            break;
        case "-h" or "--help" or "help":
            return Help();
        default:
            return Fail($"unknown option: {args[i]} (try --help)");
    }
}

long freed = 0;
if (doBackups) freed += CleanBackups();
if (doShots) freed += CleanShots();

if (dryRun) Note($"Dry run — nothing deleted. {Human(freed)} would be freed.");
else if (freed == 0) Ok("Nothing to clean.");
else Ok($"Freed {Human(freed)}.");
return 0;

// ---------------------------------------------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------------------------------------------

// Newest-first by write time, not by the timestamp in the name: a restored or copied-in file should be judged
// by when it actually landed here.
long CleanBackups()
{
    if (!Directory.Exists(backupDir))
    {
        Note("device-backups/ — nothing there yet.");
        return 0;
    }

    var all = new DirectoryInfo(backupDir)
        .GetFiles(BackupPattern)
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .ToList();

    if (all.Count == 0)
    {
        Note("device-backups/ — no backups to clean.");
        return 0;
    }

    var kept = all.Take(keep).ToList();
    var doomed = all.Skip(keep).ToList();

    Note($"device-backups/ — {all.Count} backup(s), keeping the {kept.Count} newest:");
    foreach (var f in kept) Console.WriteLine($"    keep    {f.Name}  {Human(f.Length)}  {f.LastWriteTime:yyyy-MM-dd HH:mm}");
    if (keep == 0)
        Warn("--keep 0: every backup is being removed, leaving no restore point if a redeploy reinstalls the app.");

    return Remove(doomed);
}

long CleanShots()
{
    if (!Directory.Exists(shotsDir))
    {
        Note("playwright/shots/ — nothing there yet.");
        return 0;
    }

    // Screenshots are pure output — a walker regenerates them on its next run — so there's nothing to keep.
    var doomed = new DirectoryInfo(shotsDir)
        .GetFiles()
        .Where(f => shotExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
        .OrderBy(f => f.Name)
        .ToList();

    Note($"playwright/shots/ — {doomed.Count} screenshot(s) to clear.");
    return Remove(doomed);
}

// ---------------------------------------------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------------------------------------------

long Remove(List<FileInfo> files)
{
    long freedHere = 0;
    foreach (var f in files)
    {
        long size = f.Length;   // read before the delete; FileInfo.Length throws once the file is gone
        if (dryRun)
        {
            Console.WriteLine($"    would delete  {f.Name}  {Human(size)}");
            freedHere += size;
            continue;
        }

        try
        {
            f.Delete();
            Console.WriteLine($"    deleted  {f.Name}  {Human(size)}");
            freedHere += size;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // One stubborn file (open in a viewer, permissions) shouldn't abort the rest of the sweep.
            Warn($"couldn't delete {f.Name}: {ex.Message}");
        }
    }
    return freedHere;
}

int Help()
{
    Console.WriteLine("""
        cleanup-scratch — clear the gitignored scratch folders (device-backups/, playwright/shots/).

          dotnet run scripts/cleanup-scratch.cs --                 keep the 3 newest backups, clear all shots
          dotnet run scripts/cleanup-scratch.cs -- --dry-run       show what would go, delete nothing
          dotnet run scripts/cleanup-scratch.cs -- --keep 5        keep the 5 newest backups
          dotnet run scripts/cleanup-scratch.cs -- --backups-only  leave shots/ alone
          dotnet run scripts/cleanup-scratch.cs -- --shots-only    leave device-backups/ alone

        Backups are the only safety net against a redeploy that reinstalls the app, so the newest --keep are
        always retained. Screenshots are regenerated by the walkers, so all of them go.
        """);
    return 0;
}

int Fail(string message) { Die(message); return 1; }

static string Human(long bytes)
{
    string[] units = ["B", "KB", "MB", "GB", "TB"];
    double b = bytes;
    int i = 0;
    while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
    return $"{b:0.#} {units[i]}";
}

void Note(string m) => WriteColor(ConsoleColor.Cyan, m, Console.Out);
void Ok(string m) => WriteColor(ConsoleColor.Green, m, Console.Out);
void Warn(string m) => WriteColor(ConsoleColor.Yellow, "warning: " + m, Console.Error);
void Die(string m) { WriteColor(ConsoleColor.Red, "error: " + m, Console.Error); Environment.Exit(1); }

static void WriteColor(ConsoleColor color, string message, TextWriter writer)
{
    // Console color APIs are cross-platform and no-op gracefully when output is redirected.
    var prev = Console.ForegroundColor;
    try { Console.ForegroundColor = color; writer.WriteLine(message); }
    finally { Console.ForegroundColor = prev; }
}

// Captures this source file's own path at compile time so paths resolve independently of the working directory.
static class ScriptLocation
{
    public static string File([CallerFilePath] string path = "") => path;
}
