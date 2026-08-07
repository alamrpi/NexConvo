using System.Text;
using FluentAssertions;
using NexConvo.Knowledge.Infrastructure.TextExtraction;

namespace NexConvo.Knowledge.Infrastructure.Tests;

public class PlainTextExtractorTests
{
    private readonly PlainTextExtractor _sut = new();

    [Fact]
    public async Task ExtractTextAsync_Utf8Stream_ReturnsDecodedText()
    {
        const string expected = "Hello world. আমার নাম রহিম।";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(expected));

        var result = await _sut.ExtractTextAsync(stream, CancellationToken.None);

        result.Should().Be(expected);
    }
}
