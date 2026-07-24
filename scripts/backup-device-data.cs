#!/usr/bin/env dotnet
// backup-device-data.cs — pull (and restore) KHost Cue's on-device data off a connected Android device, so a
// redeploy that reinstalls the app (signing-key mismatch, a package change, or a manual uninstall-to-fix) can't
// silently wipe your singers / song lists / tonight sets / venues / settings.
//
// Portable by design: a .NET 10 *file-based app*. Every contributor already has the .NET SDK (it's required to build
// the app), so this runs identically on Windows, Linux, and macOS with no extra dependency — no bash, no gzip/tar
// binaries on the host (we tar/gunzip in-process via System.Formats.Tar + System.IO.Compression). The only external
// tool is `adb`, which is already needed for any on-device work and behaves the same on all three OSes.
//
// WHY THIS EXISTS: the host test suites (Unit + Integration) never touch a real device — they run against a throwaway
// temp folder. The data-loss risk is a *device deploy that reinstalls*, which drops /data/data/khost.mobile. Run a
// backup before any risky redeploy or troubleshooting reinstall.
//
// HOW IT WORKS: the Debug build is debuggable, so `adb run-as khost.mobile` reads the app's private data dir without
// root. We stream a tar of files/ + shared_prefs/ off the device and gzip it on the host. Backups land in
// device-backups/ (gitignored) so real singer data never reaches git.
//
// Usage:
//   dotnet run scripts/backup-device-data.cs -- backup                 # timestamped .tar.gz -> device-backups/
//   dotnet run scripts/backup-device-data.cs -- list                   # list local backups
//   dotnet run scripts/backup-device-data.cs -- inspect <file.tar.gz>  # show what's inside a backup
//   dotnet run scripts/backup-device-data.cs -- restore <file.tar.gz>  # push a backup back onto the device
//   (On Unix you can also `chmod +x` this file and run ./scripts/backup-device-data.cs -- backup)
//
// Options (any subcommand):
//   -s, --serial <serial>   target a specific device when more than one is attached (also honors $ANDROID_SERIAL)
//   restore also takes:
//   -y, --yes               skip the "this overwrites device data" confirmation

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.CompilerServices;

const string Pkg = "khost.mobile";
const string DeviceDataDir = "/data/data/khost.mobile";
const string DeviceTmp = "/data/local/tmp/khost-restore.tar";
const string SingersEntry = "files/singers.json";   // the sentinel we verify every backup actually captured

// --- resolve paths relative to THIS script's location, so it works regardless of the current directory ---
string scriptDir = Path.GetDirectoryName(ScriptLocation.File()) is { Length: > 0 } d && Directory.Exists(d)
    ? d
    : Directory.GetCurrentDirectory();
string repoRoot = Path.GetDirectoryName(scriptDir) ?? scriptDir;
string backupDir = Path.Combine(repoRoot, "device-backups");

// --serial defaults to $ANDROID_SERIAL; overridden by the flag.
string? serial = Environment.GetEnvironmentVariable("ANDROID_SERIAL") is { Length: > 0 } s ? s : null;

// --- parse args: first bare token is the subcommand, the next is a file; flags can appear anywhere ---
string? sub = null, file = null;
bool assumeYes = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-s" or "--serial":
            if (i + 1 >= args.Length) Die("--serial needs a value.");
            serial = args[++i];
            break;
        case "-y" or "--yes":
            assumeYes = true;
            break;
        case "-h" or "--help" or "help" when sub is null:
            sub = "help";
            break;
        default:
            if (sub is null) sub = args[i];
            else if (file is null) file = args[i];
            break;
    }
}

return (sub ?? "help") switch
{
    "backup" => Backup(),
    "list" => List(),
    "inspect" => Inspect(file),
    "restore" => Restore(file, assumeYes),
    "help" => Help(),
    var other => Fail($"unknown command: {other} (try: backup | restore | list | inspect | help)"),
};

// ---------------------------------------------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------------------------------------------

int Backup()
{
    RequireDevice();
    RequireRunAs();
    Directory.CreateDirectory(backupDir);

    string ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    string outPath = Path.Combine(backupDir, $"khost-mobile-{ts}.tar.gz");
    Note($"Backing up {Pkg} data from device -> {Rel(outPath)}");

    // Stream the device's tar straight into a gzip file on the host. .corrupt quarantine files (recoverable) come
    // along for free since they live under files/. On any failure, don't leave a truncated .tar.gz behind.
    try
    {
        int code = RunAdbToStream(
            ["exec-out", "run-as", Pkg, "tar", "-c", "-C", DeviceDataDir, "files", "shared_prefs"],
            deviceStdout =>
            {
                using var fs = File.Create(outPath);
                using var gz = new GZipStream(fs, CompressionLevel.Optimal);
                deviceStdout.CopyTo(gz);
            },
            out string stderr);

        if (code != 0)
        {
            TryDelete(outPath);
            return Fail($"backup failed (adb exit {code}). {stderr.Trim()}");
        }
    }
    catch (Exception ex)
    {
        TryDelete(outPath);
        return Fail($"backup failed: {ex.Message}");
    }

    var info = new FileInfo(outPath);
    if (!info.Exists || info.Length == 0)
    {
        TryDelete(outPath);
        return Fail("backup produced an empty file.");
    }

    // Verify the payload actually holds the real data files before we trust it.
    var entries = ReadTarEntries(outPath);
    bool hasSingers = entries.Any(e => e.Name == SingersEntry);
    if (!hasSingers)
        Warn($"backup does not contain {SingersEntry} — the app may have no data yet, or the layout changed.");

    Ok($"Backup complete ({Human(info.Length)}). Files captured:");
    foreach (var e in entries.Where(e => e.Name.EndsWith(".json") || e.Name.EndsWith(".xml")))
        Console.WriteLine($"  {e.Name}");
    return 0;
}

int List()
{
    if (!Directory.Exists(backupDir))
    {
        Note($"No backups yet ({Rel(backupDir)}/ does not exist).");
        return 0;
    }
    var files = new DirectoryInfo(backupDir)
        .GetFiles("khost-mobile-*.tar.gz")
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .ToList();
    if (files.Count == 0)
    {
        Note($"No backups yet in {Rel(backupDir)}/.");
        return 0;
    }
    Note($"Backups in {Rel(backupDir)}/ (newest first):");
    foreach (var f in files)
        Console.WriteLine($"  {Human(f.Length),9}  {f.LastWriteTime:yyyy-MM-dd HH:mm}  {f.Name}");
    return 0;
}

int Inspect(string? path)
{
    if (string.IsNullOrEmpty(path)) return Fail("usage: inspect <file.tar.gz>");
    if (!File.Exists(path)) return Fail($"no such file: {path}");
    Note($"Contents of {path}:");
    foreach (var e in ReadTarEntries(path))
        Console.WriteLine($"  {Human(e.Length),9}  {e.Name}");
    return 0;
}

int Restore(string? path, bool yes)
{
    if (string.IsNullOrEmpty(path)) return Fail("usage: restore <file.tar.gz> [--yes]");
    if (!File.Exists(path)) return Fail($"no such backup: {path}");

    RequireDevice();
    RequireRunAs();

    Warn($"Restore OVERWRITES the current on-device data for {Pkg} with the contents of:");
    Console.Error.WriteLine($"  {path}");
    if (!yes)
    {
        Console.Write("Type 'restore' to proceed: ");
        if (Console.ReadLine()?.Trim() != "restore") return Fail("aborted.");
    }

    // Stop the app first so it can't overwrite shared_prefs on exit or hold files open.
    Note($"Stopping {Pkg}…");
    RunAdb(["shell", "am", "force-stop", Pkg]);

    // Decompress on the host to a plain tar, push it somewhere run-as can read, extract in place, clean up.
    string tmp = Path.Combine(Path.GetTempPath(), $"khost-restore-{Guid.NewGuid():N}.tar");
    try
    {
        using (var fs = File.OpenRead(path))
        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
        using (var outFs = File.Create(tmp))
            gz.CopyTo(outFs);

        Note("Pushing backup to device…");
        if (RunAdb(["push", tmp, DeviceTmp]).code != 0) return Fail("adb push failed.");

        Note($"Extracting into {DeviceDataDir}…");
        var (code, _, stderr) = RunAdb(["shell", "run-as", Pkg, "tar", "-x", "-C", DeviceDataDir, "-f", DeviceTmp]);
        if (code != 0) return Fail($"extract failed — device data may be partially restored; re-run restore. {stderr.Trim()}");

        RunAdb(["shell", "rm", "-f", DeviceTmp]);
    }
    finally
    {
        TryDelete(tmp);
    }

    Ok("Restore complete. Relaunch KHost Cue to see the restored data.");
    return 0;
}

int Help()
{
    // Echo the header comment block (the lines between the shebang and the first `using`) as the help text.
    foreach (var line in File.ReadLines(ScriptLocation.File()))
    {
        if (line.StartsWith("#!")) continue;
        if (line.StartsWith("using ")) break;
        Console.WriteLine(line.StartsWith("// ") ? line[3..] : line.StartsWith("//") ? line[2..] : line);
    }
    return 0;
}

// ---------------------------------------------------------------------------------------------------------------
// Device / adb helpers
// ---------------------------------------------------------------------------------------------------------------

void RequireDevice()
{
    // Count devices in the 'device' state (ignore 'offline'/'unauthorized').
    var (code, stdout, _) = RunAdb(["devices"]);
    if (code != 0)
        Die("could not run `adb devices` — is adb on PATH?");
    var ready = stdout.Split('\n')
        .Skip(1)
        .Select(l => l.Trim())
        .Where(l => l.EndsWith("\tdevice"))
        .Select(l => l.Split('\t')[0])
        .ToList();
    if (ready.Count == 0)
        Die("no device in 'device' state. For wireless adb: adb connect <ip>:<port>, then re-run.");
    if (serial is null && ready.Count > 1)
        Die($"multiple devices attached; pass --serial <serial> (one of: {string.Join(", ", ready)}).");
}

void RequireRunAs()
{
    // Confirms the app is installed AND debuggable — run-as only works on debuggable (Debug) builds.
    if (RunAdb(["exec-out", "run-as", Pkg, "id"]).code != 0)
        Die($"run-as {Pkg} failed. Is a *Debug* build of KHost Cue installed on this device? " +
            "(Release builds aren't debuggable, so their data can't be pulled without root.)");
}

// Run adb with the selected serial and capture text stdout+stderr. Adb-not-found is a fatal, friendly error.
(int code, string stdout, string stderr) RunAdb(string[] adbArgs)
{
    var psi = new ProcessStartInfo("adb") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var a in WithSerial(adbArgs)) psi.ArgumentList.Add(a);
    Process p;
    try { p = Process.Start(psi)!; }
    catch (System.ComponentModel.Win32Exception) { Die("adb not found on PATH. Install platform-tools and retry."); throw; }
    // Read both pipes concurrently to avoid a full-buffer deadlock.
    var outTask = p.StandardOutput.ReadToEndAsync();
    var errTask = p.StandardError.ReadToEndAsync();
    p.WaitForExit();
    return (p.ExitCode, outTask.Result, errTask.Result);
}

// Run adb and hand the raw binary stdout stream to `consume` (used for the tar stream). Returns the exit code and
// captures stderr text. We must read stdout as bytes, not text, so the tar isn't corrupted by encoding.
int RunAdbToStream(string[] adbArgs, Action<Stream> consume, out string stderr)
{
    var psi = new ProcessStartInfo("adb") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var a in WithSerial(adbArgs)) psi.ArgumentList.Add(a);
    Process p;
    try { p = Process.Start(psi)!; }
    catch (System.ComponentModel.Win32Exception) { Die("adb not found on PATH. Install platform-tools and retry."); throw; }
    var errTask = p.StandardError.ReadToEndAsync();
    consume(p.StandardOutput.BaseStream);
    p.WaitForExit();
    stderr = errTask.Result;
    return p.ExitCode;
}

IEnumerable<string> WithSerial(string[] adbArgs) =>
    serial is null ? adbArgs : new[] { "-s", serial }.Concat(adbArgs);

// ---------------------------------------------------------------------------------------------------------------
// Small utilities
// ---------------------------------------------------------------------------------------------------------------

// List a .tar.gz's entries (name + size) without extracting — in-process, no host tar/gzip needed.
List<(string Name, long Length)> ReadTarEntries(string gzPath)
{
    var result = new List<(string, long)>();
    using var fs = File.OpenRead(gzPath);
    using var gz = new GZipStream(fs, CompressionMode.Decompress);
    using var tar = new TarReader(gz);
    while (tar.GetNextEntry() is { } entry)
        result.Add((entry.Name, entry.Length));
    return result;
}

string Rel(string path) => Path.GetRelativePath(repoRoot, path);

void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ } }

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
