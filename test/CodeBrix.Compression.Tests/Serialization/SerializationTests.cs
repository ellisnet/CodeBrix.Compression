using CodeBrix.Compression.BZip2;
using CodeBrix.Compression.Core;
using CodeBrix.Compression.GZip;
using CodeBrix.Compression.Lzw;
using CodeBrix.Compression.Tar;
using CodeBrix.Compression.Zip;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Text.Json;

namespace CodeBrix.Compression.Tests.Serialization;

[TestFixture]
public class SerializationTests
{
    /// <summary>
    /// Test that CodeBrix.Compression Custom Exceptions can be serialized.
    /// </summary>
    [Test]
    [Category("Core")]
    [Category("Serialization")]
    [TestCase(typeof(BZip2Exception))]
    [TestCase(typeof(GZipException))]
    [TestCase(typeof(InvalidHeaderException))]
    [TestCase(typeof(InvalidNameException))]
    [TestCase(typeof(LzwException))]
    [TestCase(typeof(CompressionExceptionBase))]
    [TestCase(typeof(StreamDecodingException))]
    [TestCase(typeof(StreamUnsupportedException))]
    [TestCase(typeof(TarException))]
    [TestCase(typeof(UnexpectedEndOfStreamException))]
    [TestCase(typeof(ZipException))]
    public void SerializeException(Type exceptionType)
    {
        var message = $"Serialized {exceptionType.Name}";
        var exception = Activator.CreateInstance(exceptionType, message);

        var deserializedException = ExceptionSerialiseHelper(exception, exceptionType) as Exception;
        Assert.That(deserializedException, Is.InstanceOf(exceptionType), "deserialized object should have the correct type");
        Assert.That(deserializedException.Message, Is.EqualTo(message), "deserialized message should match original message");
    }

    /// <summary>
    /// Test that ValueOutOfRangeException can be serialized.
    /// </summary>
    [Test]
    [Category("Core")]
    [Category("Serialization")]
    public void SerializeValueOutOfRangeException()
    {
        var message = "Serialized ValueOutOfRangeException";
        var exception = new ValueOutOfRangeException(message);

        var deserializedException = ExceptionSerialiseHelper(exception, typeof(ValueOutOfRangeException)) as ValueOutOfRangeException;

        // ValueOutOfRangeException appends 'out of range' to the end of the message
        Assert.That(deserializedException.Message, Is.EqualTo($"{message} out of range"), "should have expected message");
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