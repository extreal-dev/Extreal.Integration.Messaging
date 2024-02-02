using System;
using UniRx;
using NUnit.Framework;
using System.Diagnostics.CodeAnalysis;
using UnityEngine.TestTools;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Text.RegularExpressions;
using Extreal.Core.Logging;

namespace Extreal.Integration.Messaging.Test
{
    public class MessagingClientTest
    {
        private MessagingClientMock messagingClient;

        private readonly string localClientId = nameof(localClientId);
        private readonly string otherClientId = nameof(otherClientId);

        [SuppressMessage("CodeCracker", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        [SetUp]
        public void Initialize()
        {
            LoggingManager.Initialize(LogLevel.Debug);

            messagingClient = new MessagingClientMock().AddTo(disposables);
        }

        [TearDown]
        public void Dispose()
        {
            disposables.Clear();
            messagingClient = null;
        }

        [OneTimeTearDown]
        public void OneTimeDispose()
            => disposables.Dispose();

        [UnityTest]
        public IEnumerator SendMessageNull() => UniTask.ToCoroutine(async () =>
        {
            var exception = default(Exception);
            try
            {
                await messagingClient.SendMessageAsync(null);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.GetType(), Is.EqualTo(typeof(ArgumentNullException)));
            Assert.That(exception.Message, Does.Contain("message"));
        });

        [UnityTest]
        public IEnumerator SendMessageBeforeJoiningGroup() => UniTask.ToCoroutine(async () =>
        {
            const string message = "TestMessage";
            await messagingClient.SendMessageAsync(message);

            LogAssert.Expect(LogType.Warning, new Regex(".*Called Send method before joining a group.*"));
        });
    }
}
