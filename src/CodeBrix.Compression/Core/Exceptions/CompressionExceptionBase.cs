using System;

namespace CodeBrix.Compression;

/// <summary>
/// CompressionExceptionBase is the base exception class for CodeBrix.Compression.
/// All library exceptions are derived from this.
/// </summary>
/// <remarks>NOTE: Not all exceptions thrown will be derived from this class.
/// A variety of other exceptions are possible for example <see cref="ArgumentNullException"></see></remarks>
public class CompressionExceptionBase : Exception
{
    /// <summary>
    /// Initializes a new instance of the SharpZipBaseException class.
    /// </summary>
    public CompressionExceptionBase()
    {
    }

    /// <summary>
    /// Initializes a new instance of the SharpZipBaseException class with a specified error message.
    /// </summary>
    /// <param name="message">A message describing the exception.</param>
    public CompressionExceptionBase(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the SharpZipBaseException class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">A message describing the exception.</param>
    /// <param name="innerException">The inner exception</param>
    public CompressionExceptionBase(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}