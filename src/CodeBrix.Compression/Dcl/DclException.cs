using System;

namespace CodeBrix.Compression.Dcl;

/// <summary>
/// DclException represents exceptions specific to DCL (PKWARE Data Compression Library
/// "implode" format) classes and code.
/// </summary>
public class DclException : CompressionExceptionBase
{
    /// <summary>
    /// Initialise a new instance of <see cref="DclException" />.
    /// </summary>
    public DclException()
    {
    }

    /// <summary>
    /// Initialise a new instance of <see cref="DclException" /> with its message string.
    /// </summary>
    /// <param name="message">A <see cref="string"/> that describes the error.</param>
    public DclException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialise a new instance of <see cref="DclException" />.
    /// </summary>
    /// <param name="message">A <see cref="string"/> that describes the error.</param>
    /// <param name="innerException">The <see cref="Exception"/> that caused this exception.</param>
    public DclException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
