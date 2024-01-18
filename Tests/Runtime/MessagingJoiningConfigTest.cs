using System;
using NUnit.Framework;

namespace Extreal.Integration.Messaging.Test
{
    public class MessagingJoiningConfigTest
    {
        [Test]
        public void MessagingJoiningConfigWithGroupNameNull()
            => Assert.That(() => _ = new MessagingJoiningConfig(null),
                Throws.TypeOf<ArgumentNullException>()
                    .With.Message.Contains("groupName"));
    }
}
