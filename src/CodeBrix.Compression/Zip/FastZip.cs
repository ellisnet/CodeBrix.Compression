using CodeBrix.Compression.Core;
using System;
using System.IO;
using static CodeBrix.Compression.Zip.Compression.Deflater;
using static CodeBrix.Compression.Zip.ZipEntryFactory;

namespace CodeBrix.Compression.Zip;

/// <summary>
/// FastZipEvents supports all events applicable to <see cref="FastZip">FastZip</see> operations.
/// </summary>
public class FastZipEvents
{
    /// <summary>
    /// Delegate to invoke when processing directories.
    /// </summary>
    public event EventHandler<DirectoryEventArgs> ProcessDirectory;

    /// <summary>
    /// Delegate to invoke when processing files.
    /// </summary>
    public ProcessFileHandler ProcessFile;

    /// <summary>
    /// Delegate to invoke during processing of files.
    /// </summary>
    public ProgressHandler Progress;

    /// <summary>
    /// Delegate to invoke when processing for a file has been completed.
    /// </summary>
    public CompletedFileHandler CompletedFile;

    /// <summary>
    /// Delegate to invoke when processing directory failures.
    /// </summary>
    public DirectoryFailureHandler DirectoryFailure;

    /// <summary>
    /// Delegate to invoke when processing file failures.
    /// </summary>
    public FileFailureHandler FileFailure;

    /// <summary>
    /// Raise the <see cref="DirectoryFailure">directory failure</see> event.
    /// </summary>
    /// <param name="directory">The directory causing the failure.</param>
    /// <param name="e">The exception for this event.</param>
    /// <returns>A boolean indicating if execution should continue or not.</returns>
    public bool OnDirectoryFailure(string directory, Exception e)
    {
        var result = false;
        var handler = DirectoryFailure;

        if (handler != null)
        {
            var args = new ScanFailureEventArgs(directory, e);
            handler(this, args);
            result = args.ContinueRunning;
        }
        return result;
    }

    /// <summary>
    /// Fires the <see cref="FileFailure"> file failure handler delegate</see>.
    /// </summary>
    /// <param name="file">The file causing the failure.</param>
    /// <param name="e">The exception for this failure.</param>
    /// <returns>A boolean indicating if execution should continue or not.</returns>
    public bool OnFileFailure(string file, Exception e)
    {
        var handler = FileFailure;
        var result = (handler != null);

        if (result)
        {
            var args = new ScanFailureEventArgs(file, e);
            handler(this, args);
            result = args.ContinueRunning;
        }
        return result;
    }

    /// <summary>
    /// Fires the <see cref="ProcessFile">ProcessFile delegate</see>.
    /// </summary>
    /// <param name="file">The file being processed.</param>
    /// <returns>A boolean indicating if execution should continue or not.</returns>
    public bool OnProcessFile(string file)
    {
        var result = true;
        var handler = ProcessFile;

        if (handler != null)
        {
            var args = new ScanEventArgs(file);
            handler(this, args);
            result = args.ContinueRunning;
        }
        return result;
    }

    /// <summary>
    /// Fires the <see cref="CompletedFile"/> delegate
    /// </summary>
    /// <param name="file">The file whose processing has been completed.</param>
    /// <returns>A boolean indicating if execution should continue or not.</returns>
    public bool OnCompletedFile(string file)
    {
        var result = true;
        var handler = CompletedFile;
        if (handler != null)
        {
            var args = new ScanEventArgs(file);
            handler(this, args);
            result = args.ContinueRunning;
        }
        return result;
    }

    /// <summary>
    /// Fires the <see cref="ProcessDirectory">process directory</see> delegate.
    /// </summary>
    /// <param name="directory">The directory being processed.</param>
    /// <param name="hasMatchingFiles">Flag indicating if the directory has matching files as determined by the current filter.</param>
    /// <returns>A <see cref="bool"/> of true if the operation should continue; false otherwise.</returns>
    public bool OnProcessDirectory(string directory, bool hasMatchingFiles)
    {
        var result = true;
        var handler = ProcessDirectory;
        if (handler != null)
        {
            var args = new DirectoryEventArgs(directory, hasMatchingFiles);
            handler(this, args);
            result = args.ContinueRunning;
        }
        return result;
    }

    /// <summary>
    /// The minimum timespan between <see cref="Progress"/> events.
    /// </summary>
    /// <value>The minimum period of time between <see cref="Progress"/> events.</value>
    /// <seealso cref="Progress"/>
    /// <remarks>The default interval is three seconds.</remarks>
    public TimeSpan ProgressInterval
    {
        get { return progressInterval_; }
        set { progressInterval_ = value; }
    }

    #region Instance Fields

    private TimeSpan progressInterval_ = TimeSpan.FromSeconds(3);

    #endregion Instance Fields
}

/// <summary>
/// FastZip provides facilities for creating and extracting zip files.
/// </summary>
public class FastZip
{
    /// <summary>
    /// We will only want to allow older PkzipClassic/ZipCrypto encryption to be used if the class
    /// is under test. Otherwise, this encryption algorithm is not supported by the CodeBrix.Compression
    ///  library and should not be used.
    /// </summary>
    internal bool IsUnderTest { get; set; }

    #region Enumerations

    /// <summary>
    /// Defines the desired handling when overwriting files during extraction.
    /// </summary>
    public enum Overwrite
    {
        /// <summary>
        /// Prompt the user to confirm overwriting
        /// </summary>
        Prompt,

        /// <summary>
        /// Never overwrite files.
        /// </summary>
        Never,

        /// <summary>
        /// Always overwrite files.
        /// </summary>
        Always
    }

    #endregion Enumerations

    #region Constructors

    /// <summary>
    /// Initialise a default instance of <see cref="FastZip"/>.
    /// </summary>
    public FastZip()
    {
    }

    /// <summary>
    /// Initialise a new instance of <see cref="FastZip"/> using the specified <see cref="TimeSetting"/>
    /// </summary>
    /// <param name="timeSetting">The <see cref="TimeSetting">time setting</see> to use when creating or extracting <see cref="ZipEntry">Zip entries</see>.</param>
    /// <remarks>Using <see cref="TimeSetting.LastAccessTime">TimeSetting.LastAccessTime</see><see cref="TimeSetting.LastAccessTimeUtc">[Utc]</see> when
    /// creating an archive will set the file time to the moment of reading.
    /// </remarks>
    public FastZip(TimeSetting timeSetting)
    {
        _entryFactory = new ZipEntryFactory(timeSetting);
        _restoreDateTimeOnExtract = true;
    }

    /// <summary>
    /// Initialise a new instance of <see cref="FastZip"/> using the specified <see cref="DateTime"/>
    /// </summary>
    /// <param name="time">The time to set all <see cref="ZipEntry.DateTime"/> values for created or extracted <see cref="ZipEntry">Zip Entries</see>.</param>
    public FastZip(DateTime time)
    {
        _entryFactory = new ZipEntryFactory(time);
        _restoreDateTimeOnExtract = true;
    }

    /// <summary>
    /// Initialise a new instance of <see cref="FastZip"/>
    /// </summary>
    /// <param name="events">The <see cref="FastZipEvents">events</see> to use during operations.</param>
    public FastZip(FastZipEvents events)
    {
        _events = events;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Get/set a value indicating whether empty directories should be created.
    /// </summary>
    public bool CreateEmptyDirectories
    {
        get { return _createEmptyDirectories; }
        set { _createEmptyDirectories = value; }
    }

    /// <summary>
    /// Get / set the password value.
    /// </summary>
    public string Password
    {
        get { return _password; }
        set { _password = value; }
    }

    /// <summary>
    /// Get / set the method of encrypting entries.
    /// </summary>
    /// <remarks>
    /// Only applies when <see cref="Password"/> is set.
    /// </remarks>
    public ZipEncryptionMethod EntryEncryptionMethod { get; set; } = ZipEncryptionMethod.None;

    /// <summary>
    /// Get or set the <see cref="INameTransform"></see> active when creating Zip files.
    /// </summary>
    /// <seealso cref="EntryFactory"></seealso>
    public INameTransform NameTransform
    {
        get { return _entryFactory.NameTransform; }
        set
        {
            _entryFactory.NameTransform = value;
        }
    }

    /// <summary>
    /// Get or set the <see cref="IEntryFactory"></see> active when creating Zip files.
    /// </summary>
    public IEntryFactory EntryFactory
    {
        get { return _entryFactory; }
        set
        {
            if (value == null)
            {
                _entryFactory = new ZipEntryFactory();
            }
            else
            {
                _entryFactory = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the setting for <see cref="UseZip64">Zip64 handling when writing.</see>
    /// </summary>
    /// <remarks>
    /// The default value is dynamic which is not backwards compatible with old
    /// programs and can cause problems with XP's built in compression which cant
    /// read Zip64 archives. However it does avoid the situation were a large file
    /// is added and cannot be completed correctly.
    /// NOTE: Setting the size for entries before they are added is the best solution!
    /// By default the EntryFactory used by FastZip will set the file size.
    /// </remarks>
    public UseZip64 UseZip64
    {
        get { return _useZip64; }
        set { _useZip64 = value; }
    }

    /// <summary>
    /// Get/set a value indicating whether file dates and times should
    /// be restored when extracting files from an archive.
    /// </summary>
    /// <remarks>The default value is false.</remarks>
    public bool RestoreDateTimeOnExtract
    {
        get
        {
            return _restoreDateTimeOnExtract;
        }
        set
        {
            _restoreDateTimeOnExtract = value;
        }
    }

    /// <summary>
    /// Get/set a value indicating whether file attributes should
    /// be restored during extract operations
    /// </summary>
    public bool RestoreAttributesOnExtract
    {
        get { return _restoreAttributesOnExtract; }
        set { _restoreAttributesOnExtract = value; }
    }

    /// <summary>
    /// Get/set the Compression Level that will be used
    /// when creating the zip
    /// </summary>
    public CompressionLevel CompressionLevel
    {
        get { return _compressionLevel; }
        set { _compressionLevel = value; }
    }

    /// <summary>
    /// Reflects the opposite of the internal <see cref="StringCodec.ForceZipLegacyEncoding"/>, setting it to <c>false</c> overrides the encoding used for reading and writing zip entries
    /// </summary>
    public bool UseUnicode
    {
        get => !_stringCodec.ForceZipLegacyEncoding;
        set => _stringCodec.ForceZipLegacyEncoding = !value;
    }

    /// <summary> Gets or sets the code page used for reading/writing zip file entries when unicode is disabled </summary>
    public int LegacyCodePage
    {
        get => _stringCodec.CodePage;
        set => _stringCodec = StringCodec.FromCodePage(value);
    }
		
    /// <inheritdoc cref="Zip.StringCodec"/>
    public StringCodec StringCodec
    {
        get => _stringCodec;
        set => _stringCodec = value;
    }

    #endregion Properties

    #region Delegates

    /// <summary>
    /// Delegate called when confirming overwriting of files.
    /// </summary>
    public delegate bool ConfirmOverwriteDelegate(string fileName);

    #endregion Delegates

    #region CreateZip

    /// <summary>
    /// Create a zip file.
    /// </summary>
    /// <param name="zipFileName">The name of the zip file to create.</param>
    /// <param name="sourceDirectory">The directory to source files from.</param>
    /// <param name="recurse">True to recurse directories, false for no recursion.</param>
    /// <param name="fileFilter">The <see cref="PathFilter">file filter</see> to apply.</param>
    /// <param name="directoryFilter">The <see cref="PathFilter">directory filter</see> to apply.</param>
    public void CreateZip(string zipFileName, string sourceDirectory,
        bool recurse, string fileFilter, string directoryFilter)
    {
        CreateZip(File.Create(zipFileName), sourceDirectory, recurse, fileFilter, directoryFilter);
    }

    /// <summary>
    /// Create a zip file/archive.
    /// </summary>
    /// <param name="zipFileName">The name of the zip file to create.</param>
    /// <param name="sourceDirectory">The directory to obtain files and directories from.</param>
    /// <param name="recurse">True to recurse directories, false for no recursion.</param>
    /// <param name="fileFilter">The file filter to apply.</param>
    public void CreateZip(string zipFileName, string sourceDirectory, bool recurse, string fileFilter)
    {
        CreateZip(File.Create(zipFileName), sourceDirectory, recurse, fileFilter, null);
    }

    /// <summary>
    /// Create a zip archive sending output to the <paramref name="outputStream"/> passed.
    /// </summary>
    /// <param name="outputStream">The stream to write archive data to.</param>
    /// <param name="sourceDirectory">The directory to source files from.</param>
    /// <param name="recurse">True to recurse directories, false for no recursion.</param>
    /// <param name="fileFilter">The <see cref="PathFilter">file filter</see> to apply.</param>
    /// <param name="directoryFilter">The <see cref="PathFilter">directory filter</see> to apply.</param>
    /// <remarks>The <paramref name="outputStream"/> is closed after creation.</remarks>
    public void CreateZip(Stream outputStream, string sourceDirectory, bool recurse, string fileFilter, string directoryFilter)
    {
        CreateZip(outputStream, sourceDirectory, recurse, fileFilter, directoryFilter, false);
    }

    /// <summary>
    /// Create a zip archive sending output to the <paramref name="outputStream"/> passed.
    /// </summary>
    /// <param name="outputStream">The stream to write archive data to.</param>
    /// <param name="sourceDirectory">The directory to source files from.</param>
    /// <param name="recurse">True to recurse directories, false for no recursion.</param>
    /// <param name="fileFilter">The <see cref="PathFilter">file filter</see> to apply.</param>
    /// <param name="directoryFilter">The <see cref="PathFilter">directory filter</see> to apply.</param>
    /// <param name="leaveOpen">true to leave <paramref name="outputStream"/> open after the zip has been created, false to dispose it.</param>
    public void CreateZip(Stream outputStream, string sourceDirectory, bool recurse, string fileFilter, string directoryFilter, bool leaveOpen)
    {
        var scanner = new FileSystemScanner(fileFilter, directoryFilter);
        CreateZip(outputStream, sourceDirectory, recurse, scanner, leaveOpen);
    }

    /// <summary>
    /// Create a zip file.
    /// </summary>
    /// <param name="zipFileName">The name of the zip file to create.</param>
    /// <param name="sourceDirectory">The directory to source files from.</param>
    /// <param name="recurse">True to recurse directories, false for no recursion.</param>
    /// <param name="fileFilter">The <see cref="IScanFilter">file filter</see> to apply.</param>
    /// <param name="directoryFilter">The <see cref="IScanFilter">directory filter</see> to apply.</param>
    public void CreateZip(string zipFileName, string sourceDirectory,
        bool recurse, IScanFilter fileFilter, IScanFilter directoryFilter)
    {
        CreateZip(File.Create(zipFileName), sourceDirectory, recurse, fileFilter, directoryFilter, false);
    }

    /// <summary>
    /// Create a zip archive sending output to the <paramref name="outputStream"/> passed.
    /// </summary>
    /// <param name="outputStream">The stream to write archive data to.</param>
    /// <param name="sourceDirectory">The directory to source files from.</param>
    /// <param name="recurse">True to recurse directories, false for no recursion.</param>
    /// <param name="fileFilter">The <see cref="IScanFilter">file filter</see> to apply.</param>
    /// <param name="directoryFilter">The <see cref="IScanFilter">directory filter</see> to apply.</param>
    /// <param name="leaveOpen">true to leave <paramref name="outputStream"/> open after the zip has been created, false to dispose it.</param>
    public void CreateZip(Stream outputStream, string sourceDirectory, bool recurse, IScanFilter fileFilter, IScanFilter directoryFilter, bool leaveOpen = false)
    {
        var scanner = new FileSystemScanner(fileFilter, directoryFilter);
        CreateZip(outputStream, sourceDirectory, recurse, scanner, leaveOpen);
    }

    /// <summary>
    /// Create a zip archive sending output to the <paramref name="outputStream"/> passed.
    /// </summary>
    /// <param name="outputStream">The stream to write archive data to.</param>
    /// <param name="sourceDirectory">The directory to source files from.</param>
    /// <param name="recurse">True to recurse directories, false for no recursion.</param>
    /// <param name="scanner">For performing the actual file system scan</param>
    /// <param name="leaveOpen">true to leave <paramref name="outputStream"/> open after the zip has been created, false to dispose it.</param>
    /// <remarks>The <paramref name="outputStream"/> is closed after creation.</remarks>
    private void CreateZip(Stream outputStream, string sourceDirectory, bool recurse, FileSystemScanner scanner, bool leaveOpen)
    {
        NameTransform = new ZipNameTransform(sourceDirectory);
        _sourceDirectory = sourceDirectory;

        using (_outputStream = new ZipOutputStream(outputStream, _stringCodec))
        {
            if (IsUnderTest)
            {
                _outputStream.IsUnderTest = true;
            }

            _outputStream.SetLevel((int)CompressionLevel);
            _outputStream.IsStreamOwner = !leaveOpen;
            _outputStream.NameTransform = null; // all required transforms handled by us

            if (!Enum.IsDefined(EntryEncryptionMethod))
            {
                throw new NotSupportedException($"The value of {nameof(EntryEncryptionMethod)} is unexpected: {EntryEncryptionMethod}");
            }
            else if (!string.IsNullOrEmpty(_password))
            {
                if (EntryEncryptionMethod == ZipEncryptionMethod.None)
                {
                    throw new InvalidOperationException($"A password has been set but {nameof(EntryEncryptionMethod)} is set to None."
                                                        + $" Please set {nameof(EntryEncryptionMethod)} to a valid encryption method.");
                }

#pragma warning disable CS0618 // Type or member is obsolete
                if (EntryEncryptionMethod == ZipEncryptionMethod.ZipCrypto && (!IsUnderTest))
                {
                    throw new NotSupportedException($"The encryption method {EntryEncryptionMethod} is not supported by the CodeBrix.Compression library"
                                                    + " for adding files or creating new archives. Please use AES encryption methods instead.");
                }
#pragma warning restore CS0618 // Type or member is obsolete

                _outputStream.Password = _password;
            } 

            _outputStream.UseZip64 = UseZip64;
            scanner.ProcessFile += ProcessFile;
            if (CreateEmptyDirectories)
            {
                scanner.ProcessDirectory += ProcessDirectory;
            }

            if (_events != null)
            {
                if (_events.FileFailure != null)
                {
                    scanner.FileFailure += _events.FileFailure;
                }

                if (_events.DirectoryFailure != null)
                {
                    scanner.DirectoryFailure += _events.DirectoryFailure;
                }
            }

            scanner.Scan(sourceDirectory, recurse);
        }
    }

    #endregion CreateZip

    #region ExtractZip

    /// <summary>
    /// Extract the contents of a zip file.
    /// </summary>
    /// <param name="zipFileName">The zip file to extract from.</param>
    /// <param name="targetDirectory">The directory to save extracted information in.</param>
    /// <param name="fileFilter">A filter to apply to files.</param>
    public void ExtractZip(string zipFileName, string targetDirectory, string fileFilter)
    {
        ExtractZip(zipFileName, targetDirectory, Overwrite.Always, null, fileFilter, null, _restoreDateTimeOnExtract);
    }

    /// <summary>
    /// Extract the contents of a zip file.
    /// </summary>
    /// <param name="zipFileName">The zip file to extract from.</param>
    /// <param name="targetDirectory">The directory to save extracted information in.</param>
    /// <param name="overwrite">The style of <see cref="Overwrite">overwriting</see> to apply.</param>
    /// <param name="confirmDelegate">A delegate to invoke when confirming overwriting.</param>
    /// <param name="fileFilter">A filter to apply to files.</param>
    /// <param name="directoryFilter">A filter to apply to directories.</param>
    /// <param name="restoreDateTime">Flag indicating whether to restore the date and time for extracted files.</param>
    /// <param name="allowParentTraversal">Allow parent directory traversal in file paths (e.g. ../file)</param>
    public void ExtractZip(string zipFileName, string targetDirectory,
        Overwrite overwrite, ConfirmOverwriteDelegate confirmDelegate,
        string fileFilter, string directoryFilter, bool restoreDateTime, bool allowParentTraversal = false)
    {
        Stream inputStream = File.Open(zipFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        ExtractZip(inputStream, targetDirectory, overwrite, confirmDelegate, fileFilter, directoryFilter, restoreDateTime, true, allowParentTraversal);
    }

    /// <summary>
    /// Extract the contents of a zip file held in a stream.
    /// </summary>
    /// <param name="inputStream">The seekable input stream containing the zip to extract from.</param>
    /// <param name="targetDirectory">The directory to save extracted information in.</param>
    /// <param name="overwrite">The style of <see cref="Overwrite">overwriting</see> to apply.</param>
    /// <param name="confirmDelegate">A delegate to invoke when confirming overwriting.</param>
    /// <param name="fileFilter">A filter to apply to files.</param>
    /// <param name="directoryFilter">A filter to apply to directories.</param>
    /// <param name="restoreDateTime">Flag indicating whether to restore the date and time for extracted files.</param>
    /// <param name="isStreamOwner">Flag indicating whether the inputStream will be closed by this method.</param>
    /// <param name="allowParentTraversal">Allow parent directory traversal in file paths (e.g. ../file)</param>
    public void ExtractZip(Stream inputStream, string targetDirectory,
        Overwrite overwrite, ConfirmOverwriteDelegate confirmDelegate,
        string fileFilter, string directoryFilter, bool restoreDateTime,
        bool isStreamOwner, bool allowParentTraversal = false)
    {
        if ((overwrite == Overwrite.Prompt) && (confirmDelegate == null))
        {
            throw new ArgumentNullException(nameof(confirmDelegate));
        }

        _continueRunning = true;
        _overwrite = overwrite;
        _confirmDelegate = confirmDelegate;
        _extractNameTransform = new WindowsNameTransform(targetDirectory, allowParentTraversal);

        _fileFilter = new NameFilter(fileFilter);
        _directoryFilter = new NameFilter(directoryFilter);
        _restoreDateTimeOnExtract = restoreDateTime;

        using (_zipFile = new ZipFile(inputStream, !isStreamOwner, _stringCodec))
        {
            if (_password != null)
            {
                _zipFile.Password = _password;
            }

            System.Collections.IEnumerator enumerator = _zipFile.GetEnumerator();
            while (_continueRunning && enumerator.MoveNext())
            {
                var entry = (ZipEntry)enumerator.Current;
                if (entry.IsFile)
                {
                    // TODO Path.GetDirectory can fail here on invalid characters.
                    if (_directoryFilter.IsMatch(Path.GetDirectoryName(entry.Name)) && _fileFilter.IsMatch(entry.Name))
                    {
                        ExtractEntry(entry);
                    }
                }
                else if (entry.IsDirectory)
                {
                    if (_directoryFilter.IsMatch(entry.Name) && CreateEmptyDirectories)
                    {
                        ExtractEntry(entry);
                    }
                }
                else
                {
                    // Do nothing for volume labels etc...
                }
            }
        }
    }

    #endregion ExtractZip

    #region Internal Processing

    private void ProcessDirectory(object sender, DirectoryEventArgs e)
    {
        if (!e.HasMatchingFiles && CreateEmptyDirectories)
        {
            if (_events != null)
            {
                _events.OnProcessDirectory(e.Name, e.HasMatchingFiles);
            }

            if (e.ContinueRunning)
            {
                if (e.Name != _sourceDirectory)
                {
                    var entry = _entryFactory.MakeDirectoryEntry(e.Name);
                    _outputStream.PutNextEntry(entry);
                }
            }
        }
    }

    private void ProcessFile(object sender, ScanEventArgs e)
    {
        if ((_events != null) && (_events.ProcessFile != null))
        {
            _events.ProcessFile(sender, e);
        }

        if (e.ContinueRunning)
        {
            try
            {
                // The open below is equivalent to OpenRead which guarantees that if opened the
                // file will not be changed by subsequent openers, but precludes opening in some cases
                // were it could succeed. ie the open may fail as its already open for writing and the share mode should reflect that.
                using var stream = File.Open(e.Name, FileMode.Open, FileAccess.Read, FileShare.Read);
                var entry = _entryFactory.MakeFileEntry(e.Name);
                if (_stringCodec.ForceZipLegacyEncoding)
                {
                    entry.IsUnicodeText = false;
                }

                // Set up AES encryption for the entry if required.
                ConfigureEntryEncryption(entry);

                _outputStream.PutNextEntry(entry);
                AddFileContents(e.Name, stream);
            }
            catch (Exception ex)
            {
                if (_events != null)
                {
                    _continueRunning = _events.OnFileFailure(e.Name, ex);
                }
                else
                {
                    _continueRunning = false;
                    throw;
                }
            }
        }
    }

    // Set up the encryption method to use for the specific entry.
    private void ConfigureEntryEncryption(ZipEntry entry)
    {
        // Only alter the entries options if AES isn't already enabled for it
        // (it might have been set up by the entry factory, and if so we let that take precedence)
        if (!string.IsNullOrEmpty(Password) && entry.AESEncryptionStrength == 0)
        {
            switch (EntryEncryptionMethod)
            {
                case ZipEncryptionMethod.AES128:
                    entry.AESKeySize = 128;
                    break;

                case ZipEncryptionMethod.AES256:
                    entry.AESKeySize = 256;
                    break;
            }
        }
    }

    private void AddFileContents(string name, Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (_buffer == null)
        {
            _buffer = new byte[4096];
        }

        if ((_events != null) && (_events.Progress != null))
        {
            StreamUtils.Copy(stream, _outputStream, _buffer,
                _events.Progress, _events.ProgressInterval, this, name);
        }
        else
        {
            StreamUtils.Copy(stream, _outputStream, _buffer);
        }

        if (_events != null)
        {
            _continueRunning = _events.OnCompletedFile(name);
        }
    }

    private void ExtractFileEntry(ZipEntry entry, string targetName)
    {
        var proceed = true;
        if (_overwrite != Overwrite.Always)
        {
            if (File.Exists(targetName))
            {
                if ((_overwrite == Overwrite.Prompt) && (_confirmDelegate != null))
                {
                    proceed = _confirmDelegate(targetName);
                }
                else
                {
                    proceed = false;
                }
            }
        }

        if (proceed)
        {
            if (_events != null)
            {
                _continueRunning = _events.OnProcessFile(entry.Name);
            }

            if (_continueRunning)
            {
                try
                {
                    using (var outputStream = File.Create(targetName))
                    {
                        if (_buffer == null)
                        {
                            _buffer = new byte[4096];
                        }

                        using (var inputStream = _zipFile.GetInputStream(entry))
                        {
                            if ((_events != null) && (_events.Progress != null))
                            {
                                StreamUtils.Copy(inputStream, outputStream, _buffer,
                                    _events.Progress, _events.ProgressInterval, this, entry.Name, entry.Size);
                            }
                            else
                            {
                                StreamUtils.Copy(inputStream, outputStream, _buffer);
                            }
                        }

                        if (_events != null)
                        {
                            _continueRunning = _events.OnCompletedFile(entry.Name);
                        }
                    }

                    if (_restoreDateTimeOnExtract)
                    {
                        switch (_entryFactory.Setting)
                        {
                            case TimeSetting.CreateTime:
                                File.SetCreationTime(targetName, entry.DateTime);
                                break;

                            case TimeSetting.CreateTimeUtc:
                                File.SetCreationTimeUtc(targetName, entry.DateTime);
                                break;

                            case TimeSetting.LastAccessTime:
                                File.SetLastAccessTime(targetName, entry.DateTime);
                                break;

                            case TimeSetting.LastAccessTimeUtc:
                                File.SetLastAccessTimeUtc(targetName, entry.DateTime);
                                break;

                            case TimeSetting.LastWriteTime:
                                File.SetLastWriteTime(targetName, entry.DateTime);
                                break;

                            case TimeSetting.LastWriteTimeUtc:
                                File.SetLastWriteTimeUtc(targetName, entry.DateTime);
                                break;

                            case TimeSetting.Fixed:
                                File.SetLastWriteTime(targetName, _entryFactory.FixedDateTime);
                                break;

                            default:
                                throw new ZipException("Unhandled time setting in ExtractFileEntry");
                        }
                    }

                    if (RestoreAttributesOnExtract && entry.IsDOSEntry && (entry.ExternalFileAttributes != -1))
                    {
                        var fileAttributes = (FileAttributes)entry.ExternalFileAttributes;
                        // TODO: FastZip - Setting of other file attributes on extraction is a little trickier.
                        fileAttributes &= (FileAttributes.Archive | FileAttributes.Normal | FileAttributes.ReadOnly | FileAttributes.Hidden);
                        File.SetAttributes(targetName, fileAttributes);
                    }
                }
                catch (Exception ex)
                {
                    if (_events != null)
                    {
                        _continueRunning = _events.OnFileFailure(targetName, ex);
                    }
                    else
                    {
                        _continueRunning = false;
                        throw;
                    }
                }
            }
        }
    }

    private void ExtractEntry(ZipEntry entry)
    {
        var doExtraction = entry.IsCompressionMethodSupported();
        var targetName = entry.Name;

        if (doExtraction)
        {
            if (entry.IsFile)
            {
                targetName = _extractNameTransform.TransformFile(targetName);
            }
            else if (entry.IsDirectory)
            {
                targetName = _extractNameTransform.TransformDirectory(targetName);
            }

            doExtraction = !(string.IsNullOrEmpty(targetName));
        }

        // TODO: Fire delegate/throw exception were compression method not supported, or name is invalid?

        var dirName = string.Empty;

        if (doExtraction)
        {
            if (entry.IsDirectory)
            {
                dirName = targetName;
            }
            else
            {
                dirName = Path.GetDirectoryName(Path.GetFullPath(targetName));
            }
        }

        if (doExtraction && !Directory.Exists(dirName))
        {
            if (!entry.IsDirectory || CreateEmptyDirectories)
            {
                try
                {
                    _continueRunning = _events?.OnProcessDirectory(dirName, true) ?? true;
                    if (_continueRunning)
                    {
                        Directory.CreateDirectory(dirName);
                        if (entry.IsDirectory && _restoreDateTimeOnExtract)
                        {
                            switch (_entryFactory.Setting)
                            {
                                case TimeSetting.CreateTime:
                                    Directory.SetCreationTime(dirName, entry.DateTime);
                                    break;

                                case TimeSetting.CreateTimeUtc:
                                    Directory.SetCreationTimeUtc(dirName, entry.DateTime);
                                    break;

                                case TimeSetting.LastAccessTime:
                                    Directory.SetLastAccessTime(dirName, entry.DateTime);
                                    break;

                                case TimeSetting.LastAccessTimeUtc:
                                    Directory.SetLastAccessTimeUtc(dirName, entry.DateTime);
                                    break;

                                case TimeSetting.LastWriteTime:
                                    Directory.SetLastWriteTime(dirName, entry.DateTime);
                                    break;

                                case TimeSetting.LastWriteTimeUtc:
                                    Directory.SetLastWriteTimeUtc(dirName, entry.DateTime);
                                    break;

                                case TimeSetting.Fixed:
                                    Directory.SetLastWriteTime(dirName, _entryFactory.FixedDateTime);
                                    break;

                                default:
                                    throw new ZipException("Unhandled time setting in ExtractEntry");
                            }
                        }
                    }
                    else
                    {
                        doExtraction = false;
                    }
                }
                catch (Exception ex)
                {
                    doExtraction = false;
                    if (_events != null)
                    {
                        if (entry.IsDirectory)
                        {
                            _continueRunning = _events.OnDirectoryFailure(targetName, ex);
                        }
                        else
                        {
                            _continueRunning = _events.OnFileFailure(targetName, ex);
                        }
                    }
                    else
                    {
                        _continueRunning = false;
                        throw;
                    }
                }
            }
        }

        if (doExtraction && entry.IsFile)
        {
            ExtractFileEntry(entry, targetName);
        }
    }

    private static int MakeExternalAttributes(FileInfo info)
    {
        return (int)info.Attributes;
    }

    private static bool NameIsValid(string name)
    {
        return !string.IsNullOrEmpty(name) &&
               (name.IndexOfAny(Path.GetInvalidPathChars()) < 0);
    }

    #endregion Internal Processing

    #region Instance Fields

    private bool _continueRunning;
    private byte[] _buffer;
    private ZipOutputStream _outputStream;
    private ZipFile _zipFile;
    private string _sourceDirectory;
    private NameFilter _fileFilter;
    private NameFilter _directoryFilter;
    private Overwrite _overwrite;
    private ConfirmOverwriteDelegate _confirmDelegate;

    private bool _restoreDateTimeOnExtract;
    private bool _restoreAttributesOnExtract;
    private bool _createEmptyDirectories;
    private FastZipEvents _events;
    private IEntryFactory _entryFactory = new ZipEntryFactory();
    private INameTransform _extractNameTransform;
    private UseZip64 _useZip64 = UseZip64.Dynamic;
    private CompressionLevel _compressionLevel = CompressionLevel.DEFAULT_COMPRESSION;
    private StringCodec _stringCodec = ZipStrings.GetStringCodec();
    private string _password;

    #endregion Instance Fields
}