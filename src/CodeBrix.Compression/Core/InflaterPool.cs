using System;
using System.Collections.Concurrent;
using CodeBrix.Compression.Zip.Compression;

namespace CodeBrix.Compression.Core;

/// <summary>
/// Pool for <see cref="Inflater"/> instances as they can be costly due to byte array allocations.
/// </summary>
internal sealed class InflaterPool
{
    private readonly ConcurrentQueue<PooledInflater> noHeaderPool = new();
    private readonly ConcurrentQueue<PooledInflater> headerPool = new();

    internal static InflaterPool Instance { get; } = new();

    private InflaterPool()
    {
    }

    internal Inflater Rent(bool noHeader = false)
    {
        if (CompressionOptions.InflaterPoolSize <= 0)
        {
            return new Inflater(noHeader);
        }

        var pool = GetPool(noHeader);

        PooledInflater inf;
        if (pool.TryDequeue(out var inflater))
        {
            inf = inflater;
            inf.Reset();
        }
        else
        {
            inf = new PooledInflater(noHeader);
        }

        return inf;
    }

    internal void Return(Inflater inflater)
    {
        if (CompressionOptions.InflaterPoolSize <= 0)
        {
            return;
        }

        if (!(inflater is PooledInflater pooledInflater))
        {
            throw new ArgumentException("Returned inflater was not a pooled one");
        }

        var pool = GetPool(inflater.noHeader);
        if (pool.Count < CompressionOptions.InflaterPoolSize)
        {
            pooledInflater.Reset();
            pool.Enqueue(pooledInflater);
        }
    }

    private ConcurrentQueue<PooledInflater> GetPool(bool noHeader) => noHeader ? noHeaderPool : headerPool;
}