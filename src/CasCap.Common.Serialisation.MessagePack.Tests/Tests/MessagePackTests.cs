using CasCap.Common.Extensions;
using MessagePack;

namespace CasCap.Common.Serialisation.MessagePack.Tests;

public class ExtensionTests : TestBase
{
    public ExtensionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void TestExtensions()
    {
        //Arrange
        var obj = new MyTestClass();

        //Act
        var bytes = obj.ToMessagePack();
        var obj2 = bytes.FromMessagePack<MyTestClass>();

        //Assert
        Assert.NotNull(obj2);
        Assert.Equal(obj, obj2);
        Assert.ThrowsAny<Exception>(() => new MyTestClass2().ToMessagePack());
        Assert.ThrowsAny<Exception>(() => (new byte[0]).FromMessagePack<MyTestClass>());
    }

    [MessagePackObject(true)]
    public class MyTestClass
    {
        public int ID { get; set; } = 1337;
        public DateTime utcNow { get; set; } = DateTime.UtcNow;

        public override bool Equals(object obj)
        {
            return obj is MyTestClass @class &&
                   ID == @class.ID &&
                   utcNow == @class.utcNow;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }

    public class MyTestClass2
    {
        public int ID2 { get; set; } = 1337;

        public MyTestClass NestedObj { get; set; } = new MyTestClass();
    }

    //internal class MyTestClass3
    //{
    //    public DateTime utcNow { get; set; } = DateTime.UtcNow;
    //    public DateTime utcNow2 { get { return utcNow; } }
    //}
}
