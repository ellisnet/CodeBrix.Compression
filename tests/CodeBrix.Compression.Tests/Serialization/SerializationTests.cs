using CodeBrix.Compression.BZip2;
using CodeBrix.Compression.Core;
using CodeBrix.Compression.GZip;
using CodeBrix.Compression.Lzw;
using CodeBrix.Compression.Tar;
using CodeBrix.Compression.Zip;
using System;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace CodeBrix.Compression.Tests.Serialization;

[Trait("Category", "Core")]
[Trait("Category", "Serialization")]
public class SerializationTests
{
    /// <summary>
    /// Test that CodeBrix.Compression Custom Exceptions can be serialized.
    /// </summary>
    [Theory]
    [InlineData(typeof(BZip2Exception))]
    [InlineData(typeof(GZipException))]
    [InlineData(typeof(InvalidHeaderException))]
    [InlineData(typeof(InvalidNameException))]
    [InlineData(typeof(LzwException))]
    [InlineData(typeof(CompressionExceptionBase))]
    [InlineData(typeof(StreamDecodingException))]
    [InlineData(typeof(StreamUnsupportedException))]
    [InlineData(typeof(TarException))]
    [InlineData(typeof(UnexpectedEndOfStreamException))]
    [InlineData(typeof(ZipException))]
    public void SerializeException(Type exceptionType)
    {
        var message = $"Serialized {exceptionType.Name}";
        var exception = Activator.CreateInstance(exceptionType, message);

        var deserializedException = ExceptionSerialiseHelper(exception, exceptionType) as Exception;
        Assert.IsAssignableFrom(exceptionType, deserializedException);
        Assert.Equal(message, deserializedException.Message);
    }

    /// <summary>
    /// Test that ValueOutOfRangeException can be serialized.
    /// </summary>
    [Fact]
    public void SerializeValueOutOfRangeException()
    {
        var message = "Serialized ValueOutOfRangeException";
        var exception = new ValueOutOfRangeException(message);

        var deserializedException = ExceptionSerialiseHelper(exception, typeof(ValueOutOfRangeException)) as ValueOutOfRangeException;

        // ValueOutOfRangeException appends 'out of range' to the end of the message
        Assert.Equal($"{message} out of range", deserializedException.Message);
    }

    // Shared serialization helper
    // Round trips the specified exception by serializing its data to JSON
    // and reconstructing the exception from the deserialized data.
    private static object ExceptionSerialiseHelper(object exception, Type exceptionType)
    {
        var ex = (Exception)exception;

        // Serialize exception data to JSON
        var data = new ExceptionData(exceptionType.AssemblyQualifiedName!, ex.Message);
        var json = JsonSerializer.Serialize(data);

        // Deserialize exception data from JSON
        var deserialized = JsonSerializer.Deserialize<ExceptionData>(json)!;

        // Reconstruct the exception from deserialized data
        var type = Type.GetType(deserialized.TypeName)!;
        var instance = (Exception)Activator.CreateInstance(type, nonPublic: true)!;

        // Set the message directly to preserve the exact serialized value
        typeof(Exception)
            .GetField("_message", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, deserialized.Message);

        return instance;
    }

    private record ExceptionData(string TypeName, string Message);
}
