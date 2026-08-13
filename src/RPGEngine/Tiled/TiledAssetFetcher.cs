namespace RPGEngine.Tiled;

/// <summary>
/// Fetches the raw bytes of a Tiled asset (a map, tileset or image) located at
/// <paramref name="uri"/>.
/// </summary>
/// <remarks>
/// <para>
/// The engine uses this delegate whenever it needs an asset that is not available
/// on the local file system. This is what makes the engine usable in environments
/// without a file system, such as a WebAssembly build running in a browser, where
/// assets are fetched over HTTP.
/// </para>
/// <para>
/// Implementations are expected to fetch <paramref name="uri"/> and return its
/// content, for example by calling
/// <c>HttpClient.GetByteArrayAsync(uri).GetAwaiter().GetResult()</c> or a
/// JavaScript interop call that downloads the URL.
/// </para>
/// </remarks>
/// <param name="uri">The absolute URI of the asset to fetch.</param>
/// <returns>The raw bytes of the asset.</returns>
public delegate byte[] TiledAssetFetcher(Uri uri);
