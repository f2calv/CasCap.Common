namespace CasCap.Common.Serialization.Tests;

/// <summary>Tests for MessagePack serialization extension methods.</summary>
public class MessagePackTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    /// <summary>Verifies MessagePack serialization and deserialization of objects via extension methods.</summary>
    [Fact]
    public void TestExtensions()
    {
        //Arrange
        var obj = new MyTestClass4();

        //Act
        var bytes = obj.ToMessagePack();
        var obj2 = bytes.FromMessagePack<MyTestClass4>();

        //Assert
        Assert.NotNull(obj2);
        Assert.Equal(obj, obj2);
        Assert.ThrowsAny<Exception>(() => new MyTestClass5().ToMessagePack());
        Assert.ThrowsAny<Exception>(() => (Array.Empty<byte>()).FromMessagePack<MyTestClass4>());
    }

    /// <summary>A test model used to verify MessagePack serialization round-trips.</summary>
    [MessagePackObject(true)]
    public class MyTestClass4
    {
        /// <summary>An integer identifier.</summary>
        public int ID { get; set; } = 1337;

        /// <summary>A UTC timestamp.</summary>
        public DateTime utcNow { get; set; } = DateTime.UtcNow;

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is MyTestClass4 @class &&
                   ID == @class.ID &&
                   utcNow == @class.utcNow;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>A test model with a nested object, used to verify MessagePack serialization failure cases.</summary>
    public class MyTestClass5
    {
        /// <summary>An integer identifier.</summary>
        public int ID2 { get; set; } = 1337;

        /// <summary>A nested <see cref="MyTestClass4"/> object.</summary>
        public MyTestClass4 NestedObj { get; set; } = new MyTestClass4();
    }
}
