namespace CasCap.Common.Extensions.Tests;

public class IOExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Fact, Trait("Category", nameof(IO))]
    public async Task IO()
    {
        //Arrange
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
        var filePath1 = Path.Combine(path, "test.txt");
        var filePath2 = Path.Combine(path, "test.bin");
        //cleanup
        if (Directory.Exists(path))
            Directory.Delete(path, true);

        //Act
        var newFolder = path.Extend("sub-folder/test.txt");
        newFolder.AppendTextFile("testing 456");
        Directory.Delete(path, true);
        await filePath1.WriteAllTextAsync("khfsjgfjsgf", CancellationToken.None);
        filePath1.AppendTextFile("testing 123");
        filePath2.WriteAllBytes(Array.Empty<byte>());

        //Assert
        Assert.True(Directory.Exists(path));
    }
}
