namespace RPGEngine.Tiled;

/// <summary>
/// Asynchronously fetches the raw bytes of a Tiled asset (a map, tileset or image) located at
/// <paramref name="uri"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the asynchronous counterpart of <see cref="TiledAssetFetcher"/>. The engine uses it
/// whenever it needs an asset that is not available on the local file system and must be fetched
/// with asynchronous I/O (e.g. certain network/browser streams that only support async reads).
/// Unlike <see cref="TiledAssetFetcher"/>, implementations never block: they return a
/// <see cref="Task{TResult}"/> that completes with the asset's bytes.
/// </para>
/// <para>
/// Implementations are expected to fetch <paramref name="uri"/> and return its content, for
/// example by calling <c>httpClient.GetByteArrayAsync(uri)</c>.
/// </para>
/// </remarks>
/// <param name="uri">The absolute URI of the asset to fetch.</param>
/// <returns>A task that resolves to the raw bytes of the asset.</returns>
public delegate Task<byte[]> TiledAssetFetcherAsync(Uri uri);
