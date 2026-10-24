namespace Blyant;

/// <summary>
/// Recovery copies of a file being edited (see Recovery): while the text has changes that are not saved, a copy is
/// written when no key has been pressed for a moment, and at least now and then while typing goes on. The copy is
/// removed when the text is saved, or when the editor is closed and the changes were given up. When the terminal
/// is closed, the last changes are copied before Blyant stops. A blank page (a text that is not a file yet) has a
/// copy of its own ("untitled-..."), until it is saved as a file
/// </summary>
public partial class TextArea
{
    /// <summary>
    /// The next editor opens the text of a recovery copy (the path of the copy): the copy is its own, removed with
    /// the changes as one it wrote itself. Taken by the constructor
    /// </summary>
    internal static string? NextRecoveredCopy { get; set; }

    // The editor that copies its text (the one waiting for keys), for the copy made when the terminal is closed
    private static volatile TextArea? _recovering;

    // True while a key is being handled: the text may be half changed, and is not copied from another thread
    private volatile bool _handlingKey;

    private bool _recoveryOwned; // There is a copy of this text (written by this editor, or opened from)
    private string _recoveryCopy = string.Empty; // The file of that copy (the file edited can be renamed, or named)
    private string _recoveryFile = string.Empty; // The file edited when that copy was written (empty: a blank page)
    private int _copiedVersion = -1; // The version of the text last copied
    private int _seenVersion = -1;
    private long _changedAt; // When the text was last changed (Environment.TickCount64)
    private long _uncopiedSince; // When the text was first changed since the last copy (0: not changed)
    private bool _recoveryFailTold; // A copy that could not be written was told (once, until one is written)
    private readonly object _recoveryLock = new();

    // Copies are made of a file in the editor, or a blank page (not of a peek, a box, the settings, or a copy)
    private bool RecoveryActive => Recovery.Enabled && _saveMode == SaveAsFile && _peek == null && !_singleLine &&
                                   !_readOnly && (FileExplorer.IsUntitled ||
                                                  (FileExplorer.CurrentFilePath.Length > 0 &&
                                                   !Recovery.IsCopy(FileExplorer.CurrentFilePath)));

    // The file of the copy of the text now: of the file, or of the blank page
    private static string CurrentCopy => FileExplorer.IsUntitled
        ? FileExplorer.UntitledCopy
        : Recovery.CopyPathOf(FileExplorer.CurrentFilePath);

    /// <summary>
    /// Watch for the terminal being closed (once, at startup): the text being edited is copied first
    /// </summary>
    internal static void WatchClose()
    {
        Recovery.WatchCloseSignals(() => _recovering?.CopyOnClose());
    }

    // Called while waiting for keys: a copy when the text has changed and a pause has come (or typing has gone on
    // for long), and none when the changes are gone (undone, saved)
    private void RecoveryTick()
    {
        if (!RecoveryActive) return;
        var version = _engine.Buffer.Version;
        if (!_engine.IsModified)
        {
            if (_recoveryOwned) RemoveRecovery();
            _copiedVersion = version;
            _uncopiedSince = 0;
            return;
        }

        if (version == _copiedVersion) return;
        var now = Environment.TickCount64;
        if (version != _seenVersion)
        {
            _seenVersion = version;
            _changedAt = now;
            if (_uncopiedSince == 0) _uncopiedSince = now;
        }

        if (now - _changedAt >= Recovery.PauseMilliseconds ||
            now - _uncopiedSince >= Recovery.MaxIntervalMilliseconds)
        {
            var status = WriteRecovery(version);
            // A copy that cannot be written is told once (the data directory may not be writable): the text is
            // not kept safe until it is saved
            if (status == DialogText.Success)
            {
                _recoveryFailTold = false;
            }
            else if (!_recoveryFailTold)
            {
                _recoveryFailTold = true;
                _engine.SetMessage(Lang.F("No recovery copy could be written: {0}", status), true);
                Draw();
            }
        }
    }

    // The copy is written (DialogText.Success), or why it could not be
    private string WriteRecovery(int version)
    {
        lock (_recoveryLock)
        {
            string copy;
            if (_recoveryOwned && _recoveryCopy.Length > 0 &&
                FileOperations.SamePath(_recoveryFile, FileExplorer.CurrentFilePath))
            {
                // The copy of this editor is written again
                copy = _recoveryCopy;
            }
            else
            {
                // Renamed in the editor: the copy goes by the new name
                if (_recoveryOwned && _recoveryCopy.Length > 0) Recovery.DeleteCopy(_recoveryCopy);
                _recoveryOwned = false;
                copy = CurrentCopy;
                // A copy that is not this editor's is never written over: one put off with "Decide later", or one
                // that another Blyant is writing. This text gets a copy beside it (offered by Recover unsaved text)
                if (File.Exists(copy)) copy = Recovery.FreeCopyPath(copy);
            }

            // (the copy written, or why it failed, is logged by FileIo; it is tried again at the next change)
            var status = Recovery.WriteCopy(copy, FileExplorer.CurrentFilePath, GetText());
            if (status == DialogText.Success)
            {
                _recoveryOwned = true;
                _recoveryCopy = copy;
                _recoveryFile = FileExplorer.CurrentFilePath;
            }

            _copiedVersion = version;
            _uncopiedSince = 0;
            return status;
        }
    }

    // The copy is no longer needed: the text is saved, or the changes were given up
    private void RemoveRecovery()
    {
        lock (_recoveryLock)
        {
            if (!_recoveryOwned) return;
            Recovery.DeleteCopy(_recoveryCopy);
            _recoveryOwned = false;
            _recoveryCopy = string.Empty;
            _recoveryFile = string.Empty;
        }
    }

    // The text is saved
    private void RecoverySaved()
    {
        if (_saveMode != SaveAsFile || _peek != null) return;
        RemoveRecovery();
        _copiedVersion = _engine.Buffer.Version;
        _uncopiedSince = 0;
    }

    // The editor is closed as the user asked (saved, or the changes given up): the copy goes. Not when the editor
    // stopped on an error and the text was not saved: then the copy is what is left of it
    private void EndRecovery()
    {
        if (_saveMode != SaveAsFile || _peek != null) return;
        RemoveRecovery();
    }

    // The terminal is closing (another thread): the latest changes are copied. A key being handled is waited for a
    // moment (the text may be half changed); if it is still being handled, the copy written before stays
    private void CopyOnClose()
    {
        var waitUntil = Environment.TickCount64 + 300;
        while (_handlingKey && Environment.TickCount64 < waitUntil) Thread.Sleep(10);
        if (_handlingKey || !RecoveryActive || !_engine.IsModified) return;
        var version = _engine.Buffer.Version;
        if (version == _copiedVersion && _recoveryOwned) return;
        try
        {
            WriteRecovery(version);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            // The text changed while it was read (a request of a key, as a snippet put in): the copy before stays
        }
    }
}
