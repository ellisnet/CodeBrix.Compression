using CodeBrix.Compression.Tar;
using CodeBrix.Compression.Tests.TestSupport;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Compression.Tests.Tar;

public class TarBufferTests
{
    [Fact]
    public void TestSimpleReadWrite()
    {
        var ms = new MemoryStream();
        var reader = TarBuffer.CreateInputTarBuffer(ms, 1);
        var writer = TarBuffer.CreateOutputTarBuffer(ms, 1);
        writer.IsStreamOwner = false;

        var block = Utils.GetDummyBytes(TarBuffer.BlockSize);

        writer.WriteBlock(block);
        writer.WriteBlock(block);
        writer.WriteBlock(block);
        writer.Close();

        ms.Seek(0, SeekOrigin.Begin);

        var block0 = reader.ReadBlock();
        var block1 = reader.ReadBlock();
        var block2 = reader.ReadBlock();
        Assert.Equal(block, block0);
        Assert.Equal(block, block1);
        Assert.Equal(block, block2);
        writer.Close();
    }

    [Fact]
    public void TestSkipBlock()
    {
        var ms = new MemoryStream();
        var reader = TarBuffer.CreateInputTarBuffer(ms, 1);
        var writer = TarBuffer.CreateOutputTarBuffer(ms, 1);
        writer.IsStreamOwner = false;

        var block0 = Utils.GetDummyBytes(TarBuffer.BlockSize);
        var block1 = Utils.GetDummyBytes(TarBuffer.BlockSize);

        writer.WriteBlock(block0);
        writer.WriteBlock(block1);
        writer.Close();

        ms.Seek(0, SeekOrigin.Begin);

        reader.SkipBlock();
        var block = reader.ReadBlock();
        Assert.Equal(block, block1);
        writer.Close();
    }

    [Fact]
    public async Task TestSimpleReadWriteAsync()
    {
        var ms = new MemoryStream();
        var reader = TarBuffer.CreateInputTarBuffer(ms, 1);
        var writer = TarBuffer.CreateOutputTarBuffer(ms, 1);
        writer.IsStreamOwner = false;

        var block = Utils.GetDummyBytes(TarBuffer.BlockSize);

        await writer.WriteBlockAsync(block, CancellationToken.None);
        await writer.WriteBlockAsync(block, CancellationToken.None);
        await writer.WriteBlockAsync(block, CancellationToken.None);
        await writer.CloseAsync(CancellationToken.None);

        ms.Seek(0, SeekOrigin.Begin);

        var block0 = new byte[TarBuffer.BlockSize];
        await reader.ReadBlockIntAsync(block0, CancellationToken.None, true);
        var block1 = new byte[TarBuffer.BlockSize];
        await reader.ReadBlockIntAsync(block1, CancellationToken.None, true);
        var block2 = new byte[TarBuffer.BlockSize];
        await reader.ReadBlockIntAsync(block2, CancellationToken.None, true);
        Assert.Equal(block, block0);
        Assert.Equal(block, block1);
        Assert.Equal(block, block2);
        await writer.CloseAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TestSkipBlockAsync()
    {
        var ms = new MemoryStream();
        var reader = TarBuffer.CreateInputTarBuffer(ms, 1);
        var writer = TarBuffer.CreateOutputTarBuffer(ms, 1);
        writer.IsStreamOwner = false;

        var block0 = Utils.GetDummyBytes(TarBuffer.BlockSize);
        var block1 = Utils.GetDummyBytes(TarBuffer.BlockSize);

        await writer.WriteBlockAsync(block0, CancellationToken.None);
        await writer.WriteBlockAsync(block1, CancellationToken.None);
        await writer.CloseAsync(CancellationToken.None);

        ms.Seek(0, SeekOrigin.Begin);

        await reader.SkipBlockAsync(CancellationToken.None);
        var block = new byte[TarBuffer.BlockSize];
        await reader.ReadBlockIntAsync(block, CancellationToken.None, true);
        Assert.Equal(block, block1);
        await writer.CloseAsync(CancellationToken.None);
    }
}
