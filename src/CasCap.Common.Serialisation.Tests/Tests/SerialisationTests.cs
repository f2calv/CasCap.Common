using CasCap.Common.Extensions;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
namespace CasCap.Common.Serialisation.Tests
{
    public class SerialisationTests : TestBase
    {
        public SerialisationTests() : base() { }

        [Fact, Trait("Category", "Serialisation"), Trait("Category", "MessagePack")]
        public void TestMessagePack()
        {
            var obj1 = new MyTestClass();
            Assert.True(IsTestClassValid(obj1));
            var str1 = obj1.ToJSON();
            Debug.WriteLine(str1);
            var bytes = obj1.ToMessagePack();

            var obj2 = bytes.FromMessagePack<MyTestClass>();
            Assert.NotNull(obj2);
            var str2 = obj2.ToJSON();
            Debug.WriteLine(str2);
            Assert.NotEqual(obj1, obj2);
            Assert.True(IsTestClassValid(obj2));

            //Debug.WriteLine(str2);
            Assert.Equal(str1, str2);
        }

        [Fact, Trait("Category", "Serialisation"), Trait("Category", "Json")]
        public void TestJson()
        {
            var obj1 = new MyTestClass();
            Assert.True(IsTestClassValid(obj1));
            var str1 = obj1.ToJSON();
            //Debug.WriteLine(str1);

            var obj2 = str1.FromJSON<MyTestClass>();
            Assert.NotNull(obj2);
            var str2 = obj2.ToJSON();
            //Debug.WriteLine(str2);
            Assert.NotEqual(obj1, obj2);
            Assert.True(IsTestClassValid(obj2));

            Assert.Equal(str1, str2);
        }

        //https://docs.microsoft.com/en-us/aspnet/core/signalr/messagepackhubprotocol?view=aspnetcore-2.2#messagepack-quirks
        /// <summary>
        /// DateTime.Kind is not preserved when serializing/deserializing - lets confirm that and implement a workaround.
        /// </summary>
        /// <returns></returns>
        [Fact, Trait("Category", "Serialisation"), Trait("Category", "MessagePack")]
        public async Task DateTimeKindSerialisation()
        {
            //todo: move this into a CasCap.Common.Serialisation.Tests lib
            await Task.Delay(0);
            var obj = new MyTestClass();
            obj.dtNowFixed = DateTime.Now;

            Assert.True(obj.dtNow.Kind == DateTimeKind.Local);
            Assert.True(obj.dtNowFixed.Kind == DateTimeKind.Utc);//Local is changed to Utc on the property set
            Assert.True(obj.utcNow.Kind == DateTimeKind.Utc);
            //obj.d.Keys.
            //myDt = DateTime.SpecifyKind(saveNow, DateTimeKind.Utc);

            var bytes = obj.ToMessagePack();

            var o = bytes.FromMessagePack<MyTestClass>();

            Assert.True(o.dtNow.Kind == DateTimeKind.Utc);//broken
            Assert.True(o.dtNowFixed.Kind == DateTimeKind.Utc);//fixed
            Assert.True(o.utcNow.Kind == DateTimeKind.Utc);//no change

            Assert.Equal(obj.dtNow, o.dtNow);
        }

        bool IsTestClassValid(MyTestClass o)
        {
            if (o.ID != 1337)
                return false;
            if (o.dtNow.Date != DateTime.UtcNow.Date)
                return false;
            if (o.d.Count != 2)
                return false;
            if (o.d[DateTime.UtcNow.Date] != "x")
                return false;
            if (o.d[DateTime.UtcNow.Date.AddDays(-1)] != "y")
                return false;
            return true;
        }
    }

    [MessagePackObject(true)]
    public class MyTestClass
    {
        public int ID { get; set; } = 1337;
        public DateTime utcNow { get; set; } = DateTime.UtcNow;
        public DateTime dtNow { get; set; } = DateTime.Now.Date;
        DateTime _dtNowFixed = DateTime.Now;
        /// <summary>
        /// We send in a normal datetime, which when deserialised by messagepack gets converted to Utc.
        /// </summary>
        public DateTime dtNowFixed
        {
            get { return _dtNowFixed; }
            set { _dtNowFixed = DateTime.SpecifyKind(value, DateTimeKind.Utc); }
        }
        public Dictionary<DateTime, string> d { get; set; } = new Dictionary<DateTime, string> { { DateTime.UtcNow.Date, "x" }, { DateTime.UtcNow.Date.AddDays(-1), "y" } };
    }
}