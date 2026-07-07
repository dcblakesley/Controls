namespace Controls;

/// <summary>
/// An <see cref="IBrowserFile"/> whose contents are held in an in-memory buffer. <see cref="EditFile"/>
/// buffers each selected file's bytes at pick time and stores one of these instead of the framework's
/// own <c>IBrowserFile</c>.
/// </summary>
/// <remarks>
/// <para>
/// Blazor resets the browser file map (<c>_blazorFilesById</c>) on <b>every</b> <c>change</c> event and
/// the native input replaces its <c>FileList</c> wholesale, so a framework <c>IBrowserFile</c> from an
/// earlier selection batch — or one still in the list after the <c>&lt;InputFile&gt;</c> unmounts at
/// <c>MaxFiles</c> — throws on <see cref="OpenReadStream"/>. A buffered copy is readable for the life of
/// the list regardless of later selections or unmounts.
/// </para>
/// <para>
/// <see cref="OpenReadStream"/> serves the full buffer and ignores its <c>maxAllowedSize</c> argument:
/// the bytes are already in memory (bounded by <c>EditFile.MaxFileSizeBytes</c> at buffer time), so the
/// framework's 500&#160;KB default guard — which exists to bound an over-the-wire read that hasn't
/// happened yet — doesn't apply. A bare <c>OpenReadStream()</c> therefore always succeeds.
/// </para>
/// </remarks>
internal sealed class BufferedBrowserFile : IBrowserFile
{
    readonly byte[] _bytes;

    public BufferedBrowserFile(IBrowserFile source, byte[] bytes)
    {
        Name = source.Name;
        LastModified = source.LastModified;
        ContentType = source.ContentType;
        _bytes = bytes;
    }

    /// <inheritdoc/>
    public string Name { get; }
    /// <inheritdoc/>
    public DateTimeOffset LastModified { get; }
    /// <inheritdoc/>
    public long Size => _bytes.LongLength;
    /// <inheritdoc/>
    public string ContentType { get; }

    /// <inheritdoc/>
    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        => new MemoryStream(_bytes, writable: false);
}
