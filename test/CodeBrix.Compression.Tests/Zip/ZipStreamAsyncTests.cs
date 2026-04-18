using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Compression.Zip;
using CodeBrix.Compression.Tests.TestSupport;
using Xunit;

namespace CodeBrix.Compression.Tests.Zip;

[Trait("Category", "Zip")]
[Trait("Category", "Async")]
public class ZipStreamAsyncTests
{
    [Fact]
    public async Task WriteZipStreamUsingAsync()
    {
        await using var ms = new MemoryStream();

        await using (var outStream = new ZipOutputStream(ms){IsStreamOwner = false})
        {
            await outStream.PutNextEntryAsync(new ZipEntry("FirstFile"));
            await Utils.WriteDummyDataAsync(outStream, 12);

            await outStream.PutNextEntryAsync(new ZipEntry("SecondFile"));
            await Utils.WriteDummyDataAsync(outStream, 12);
        }

        ZipTesting.AssertValidZip(ms);
    }

    [Fact]
    public async Task WriteZipStreamAsync ()
    {
        using var ms = new MemoryStream();

        await using(var outStream = new ZipOutputStream(ms) { IsStreamOwner = false })
        {
            await outStream.PutNextEntryAsync(new ZipEntry("FirstFile"));
            await Utils.WriteDummyDataAsync(outStream, 12);

            await outStream.PutNextEntryAsync(new ZipEntry("SecondFile"));
            await Utils.WriteDummyDataAsync(outStream, 12);

            await outStream.FinishAsync(CancellationToken.None);
        }

        ZipTesting.AssertValidZip(ms);
    }


    [Fact]
    public async Task WriteZipStreamWithAesAsync()
    {
        using var ms = new MemoryStream();
        var password = "f4ls3p0s1t1v3";

        await using (var outStream = new ZipOutputStream(ms){IsStreamOwner = false, Password = password})
        {
            await outStream.PutNextEntryAsync(new ZipEntry("FirstFile"){AESKeySize = 256});
            await Utils.WriteDummyDataAsync(outStream, 12);

            await outStream.PutNextEntryAsync(new ZipEntry("SecondFile"){AESKeySize = 256});
            await Utils.WriteDummyDataAsync(outStream, 12);

            await outStream.FinishAsync(CancellationToken.None);
        }

        ZipTesting.AssertValidZip(ms, password);
    }

    [Fact]
    public async Task WriteZipStreamWithZipCryptoAsync()
    {
        using var ms = new MemoryStream();
        var password = "f4ls3p0s1t1v3";

        await using (var outStream = new ZipOutputStream(ms){IsStreamOwner = false, Password = password})
        {
            await outStream.PutNextEntryAsync(new ZipEntry("FirstFile"){AESKeySize = 0});
            await Utils.WriteDummyDataAsync(outStream, 12);

            await outStream.PutNextEntryAsync(new ZipEntry("SecondFile"){AESKeySize = 0});
            await Utils.WriteDummyDataAsync(outStream, 12);

            await outStream.FinishAsync(CancellationToken.None);
        }

        ZipTesting.AssertValidZip(ms, password, false);
    }

    [Fact]
    public async Task WriteReadOnlyZipStreamAsync ()
    {
        await using var ms = new MemoryStreamWithoutSeek();

        await using(var outStream = new ZipOutputStream(ms) { IsStreamOwner = false })
        {
            await outStream.PutNextEntryAsync(new ZipEntry("FirstFile"));
            await Utils.WriteDummyDataAsync(outStream, 12);

            await outStream.PutNextEntryAsync(new ZipEntry("SecondFile"));
            await Utils.WriteDummyDataAsync(outStream, 12);

            await outStream.FinishAsync(CancellationToken.None);
        }

        ZipTesting.AssertValidZip(new MemoryStream(ms.ToArray()));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(12000)]
    public async Task WriteZipStreamToAsyncOnlyStream (int fileSize)
    {
        await using var ms = new MemoryStreamWithoutSync();
        await using(var outStream = new ZipOutputStream(ms) { IsStreamOwner = false })
        {
            await outStream.PutNextEntryAsync(new ZipEntry("FirstFile"));
            await Utils.WriteDummyDataAsync(outStream, fileSize);

            await outStream.PutNextEntryAsync(new ZipEntry("SecondFile"));
            await Utils.WriteDummyDataAsync(outStream, fileSize);

            await outStream.FinishAsync(CancellationToken.None);
            await outStream.DisposeAsync();
        }

        ZipTesting.AssertValidZip(new MemoryStream(ms.ToArray()));
    }
}
