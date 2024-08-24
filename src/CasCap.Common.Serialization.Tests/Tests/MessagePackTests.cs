namespace CasCap.Common.Serialization.Tests;

public class MessagePackTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
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

    [MessagePackObject(true)]
    public class MyTestClass4
    {
        public int ID { get; set; } = 1337;
        public DateTime utcNow { get; set; } = DateTime.UtcNow;

        public override bool Equals(object obj)
        {
            return obj is MyTestClass4 @class &&
                   ID == @class.ID &&
                   utcNow == @class.utcNow;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }

    public class MyTestClass5
    {
        public int ID2 { get; set; } = 1337;

        public MyTestClass4 NestedObj { get; set; } = new MyTestClass4();
    }
}
