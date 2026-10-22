using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Blyant;

/// <summary>
/// Recovery copies: the text of a file being edited that has changes not saved is copied now and then to the
/// directory "recovery" in the data directory, so it is not lost if Blyant is stopped (the terminal closed, the
/// computer turned off). A copy is removed when the text is saved or the changes are given up, so after a normal
/// session the directory is empty. When a file is opened and a copy of it is there, the user is asked what to do.
///
/// A copy is named after the file and a code made from its full path ("chapter 1.md.3f2a9c1d.txt"). It starts
/// with some lines telling which file it is a copy of, when it was written and by which process, then a line
/// "---", and then the text (with LF line endings). The same code on all systems: nothing here depends on one.
/// This class does not touch the console, so it can be tested.
/// </summary>
internal static class Recovery
{
    internal const string DirectoryName = "recovery"; // English: the name of a directory
    private const string Extension = ".txt";
    private const string Separator = "---";

    /// <summary>
    /// A copy is written when no key has been pressed for this long since the text was changed
    /// </summary>
    internal const int PauseMilliseconds = 2000;

    /// <summary>
    /// ... or when the text has been changed for this long without a pause (typing on and on)
    /// </summary>
    internal const int MaxIntervalMilliseconds = 30000;

    /// <summary>
    /// Copies are written (setting "recovery", on unless turned off). Copies that are there are offered also
    /// when it is off
    /// </summary>
    internal static bool Enabled { get; set; } = true;

    /// <summary>
    /// The directory of the copies, for tests
    /// </summary>
    internal static string? DirectoryOverride { get; set; }

    /// <summary>
    /// The directory of the copies (it is made when the first copy is written)
    /// </summary>
    internal static string Directory =>
        Path.GetFullPath(DirectoryOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), DirectoryName));

    /// <summary>
    /// A copy as read from its file
    /// </summary>
    /// <param name="CopyPath">The file of the copy</param>
    /// <param name="FilePath">The file it is a copy of</param>
    /// <param name="Written">When it was written (UTC)</param>
    /// <param name="ProcessId">The process of Blyant that wrote it</param>
    /// <param name="ProcessStarted">When that process was started (UTC), to tell it from a later one with the
    /// same number</param>
    /// <param name="Text">The text, with LF line endings</param>
    internal sealed record Copy(string CopyPath, string FilePath, DateTime Written, int ProcessId,
        DateTime ProcessStarted, string Text);

    /// <summary>
    /// The file of the copy of a file. The code is the start of the SHA-256 of the full path (in lower case where
    /// names are the same in either case), so it is the same every time and on every system
    /// </summary>
    internal static string CopyPathOf(string filePath)
    {
        var full = Path.GetFullPath(filePath);
        var key = FileOperations.NamesIgnoreCase ? full.ToLowerInvariant() : full;
        var code = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..8].ToLowerInvariant();
        return Path.Combine(Directory, Path.GetFileName(full) + "." + code + Extension);
    }

    /// <summary>
    /// True if a path is in the directory of the copies (a copy opened in the editor gets no copy of its own)
    /// </summary>
    internal static bool IsCopy(string path) =>
        FileOperations.SamePath(path, Directory) || FileOperations.IsInside(path, Directory);

    /// <summary>
    /// The content of the file of a copy
    /// </summary>
    internal static string Serialize(string filePath, string text, DateTime written)
    {
        var builder = new StringBuilder();
        // English: read by people in any language, and by Blyant
        builder.Append("# Blyant: a recovery copy of text that was not saved. The text follows the line \"---\".\n");
        builder.Append("file=").Append(Path.GetFullPath(filePath)).Append('\n');
        builder.Append("written=").Append(written.ToString("o", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("process=").Append(Environment.ProcessId.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("started=").Append(OwnStart.ToString("o", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append(Separator).Append('\n');
        builder.Append(text.Replace("\r\n", "\n").Replace('\r', '\n'));
        return builder.ToString();
    }

    /// <summary>
    /// Read the content of the file of a copy (null if it is not one)
    /// </summary>
    internal static Copy? Parse(string content, string copyPath)
    {
        content = content.Replace("\r\n", "\n");
        var values = new Dictionary<string, string>();
        var start = 0;
        while (start < content.Length)
        {
            var end = content.IndexOf('\n', start);
            if (end < 0) return null;
            var line = content[start..end];
            start = end + 1;
            if (line == Separator)
            {
                if (!values.TryGetValue("file", out var file) || file.Length == 0) return null;
                values.TryGetValue("written", out var written);
                values.TryGetValue("process", out var process);
                values.TryGetValue("started", out var started);
                return new Copy(copyPath, file, ParseTime(written), int.TryParse(process, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var id) ? id : 0, ParseTime(started), content[start..]);
            }

            var equals = line.IndexOf('=');
            if (equals > 0) values[line[..equals]] = line[(equals + 1)..];
        }

        return null;
    }

    private static DateTime ParseTime(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
            ? time.ToUniversalTime()
            : DateTime.MinValue;

    /// <summary>
    /// Write the copy of the text of a file (replacing the one before, safely). The directory is made if it is
    /// not there
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Write(string filePath, string text)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            FileIo.WriteAllBytesSafe(CopyPathOf(filePath),
                new UTF8Encoding(false).GetBytes(Serialize(filePath, text, DateTime.UtcNow)));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// The copy of a file, if there is one that can be read and is a copy of that file
    /// </summary>
    internal static Copy? Find(string filePath)
    {
        try
        {
            var path = CopyPathOf(filePath);
            if (!File.Exists(path)) return null;
            var copy = Parse(File.ReadAllText(path, Encoding.UTF8), path);
            return copy != null && FileOperations.SamePath(copy.FilePath, filePath) ? copy : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Remove the copy of a file (if there is one)
    /// </summary>
    internal static void Delete(string filePath)
    {
        try
        {
            var path = CopyPathOf(filePath);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // It is offered when the file is opened, and can be removed then
        }
    }

    // When this process was started (UTC)
    private static DateTime OwnStart => _ownStart ??= StartOf(Process.GetCurrentProcess()) ?? DateTime.UtcNow;
    private static DateTime? _ownStart;

    private static DateTime? StartOf(Process process)
    {
        try
        {
            return process.StartTime.ToUniversalTime();
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException
                                      or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// True if the copy was written by another Blyant that is still running (the file is being edited there)
    /// </summary>
    internal static bool WrittenByOtherRunning(Copy copy)
    {
        if (copy.ProcessId <= 0 || copy.ProcessId == Environment.ProcessId) return false;
        try
        {
            using var process = Process.GetProcessById(copy.ProcessId);
            // The number can have been given to another program since: it must be a Blyant (as this one is named),
            // started when the one that wrote it was (a second is allowed for how precisely the time is told)
            using var own = Process.GetCurrentProcess();
            if (!string.Equals(process.ProcessName, own.ProcessName, StringComparison.OrdinalIgnoreCase)) return false;
            return StartOf(process) is not { } started ||
                   Math.Abs((started - copy.ProcessStarted).TotalSeconds) < 1;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            // Not running
            return false;
        }
    }

    // The signals of the terminal closing and of logging off or shutting down (kept, so they stay registered)
    private static readonly List<PosixSignalRegistration> Signals = new();

    /// <summary>
    /// Call an action when the terminal is closed (SIGHUP; on Windows the window or tab is closed) or Blyant is told
    /// to stop (SIGTERM; on Windows logging off or shutting down). The action may only write files: the terminal can
    /// be gone. Blyant is stopped after it, as without it. Registered once
    /// </summary>
    internal static void WatchCloseSignals(Action action)
    {
        if (Signals.Count > 0) return;
        foreach (var signal in new[] { PosixSignal.SIGHUP, PosixSignal.SIGTERM })
        {
            try
            {
                Signals.Add(PosixSignalRegistration.Create(signal, _ =>
                {
                    try
                    {
                        action();
                    }
                    catch
                    {
                        // Blyant is stopping: there is nobody to tell
                    }
                }));
            }
            catch (Exception e) when (e is PlatformNotSupportedException or IOException)
            {
                // Not on this system: the copies written on pauses are what there is
            }
        }
    }
}
