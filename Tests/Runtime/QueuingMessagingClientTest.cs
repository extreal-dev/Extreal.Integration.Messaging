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
using System.Collections.Generic;

namespace Extreal.Integration.Messaging.Test
{
    public class QueuingMessagingClientTest
    {
        private MessagingClientMock messagingClient;
        private QueuingMessagingClient queuingMessagingClient;

        private readonly string localClientId = nameof(localClientId);
        private readonly string otherClientId = nameof(otherClientId);

        private readonly EventHandler eventHandler = new EventHandler();
        [SuppressMessage("CodeCracker", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        [SetUp]
        public void Initialize()
        {
            LoggingManager.Initialize(LogLevel.Debug);

            messagingClient = new MessagingClientMock();
            queuingMessagingClient = new QueuingMessagingClient(messagingClient).AddTo(disposables);

            queuingMessagingClient.OnJoined
                .Subscribe(eventHandler.SetClientId)
                .AddTo(disposables);

            queuingMessagingClient.OnLeaving
                .Subscribe(eventHandler.SetLeavingReason)
                .AddTo(disposables);

            queuingMessagingClient.OnUnexpectedLeft
                .Subscribe(eventHandler.SetUnexpectedLeftReason)
                .AddTo(disposables);

            queuingMessagingClient.OnJoiningApprovalRejected
                .Subscribe(_ => eventHandler.SetIsJoiningApprovalRejected(true))
                .AddTo(disposables);

            queuingMessagingClient.OnClientJoined
                .Subscribe(eventHandler.SetJoinedClientId)
                .AddTo(disposables);

            queuingMessagingClient.OnClientLeaving
                .Subscribe(eventHandler.SetLeavingClientId)
                .AddTo(disposables);
        }

        [TearDown]
        public void Dispose()
        {
            eventHandler.Clear();
            disposables.Clear();
            messagingClient = null;
            queuingMessagingClient = null;
        }

        [OneTimeTearDown]
        public void OneTimeDispose()
            => disposables.Dispose();

        [Test]
        public void NewQueuingMessagingClientWithMessagingClientNull()
            => Assert.That(() => new QueuingMessagingClient(null),
                Throws.TypeOf<ArgumentNullException>()
                    .With.Message.Contains(nameof(messagingClient)));

        [UnityTest]
        public IEnumerator ListGroupsSuccess() => UniTask.ToCoroutine(async () =>
        {
            var groups = await queuingMessagingClient.ListGroupsAsync();
            Assert.That(groups, Is.Not.Null);
            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].Name, Is.EqualTo("TestName"));
        });

        [UnityTest]
        public IEnumerator JoinSuccess() => UniTask.ToCoroutine(async () =>
        {
            var joiningConfig = new MessagingJoiningConfig("MessagingTest");

            Assert.That(eventHandler.ClientId, Is.Null);

            await queuingMessagingClient.JoinAsync(joiningConfig);

            Assert.That(eventHandler.ClientId, Is.EqualTo(localClientId));
        });

        [UnityTest]
        public IEnumerator JoinWithJoiningConfigNull() => UniTask.ToCoroutine(async () =>
        {
            var exception = default(Exception);
            try
            {
                await queuingMessagingClient.JoinAsync(null);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.GetType(), Is.EqualTo(typeof(ArgumentNullException)));
            Assert.That(exception.Message, Does.Contain("joiningConfig"));
        });

        [UnityTest]
        public IEnumerator JoiningApprovalRejected() => UniTask.ToCoroutine(async () =>
        {
            var joiningConfig = new MessagingJoiningConfig("JoiningApprovalReject");

            Assert.That(eventHandler.ClientId, Is.Null);
            Assert.That(eventHandler.IsJoiningApprovalRejected, Is.False);

            await queuingMessagingClient.JoinAsync(joiningConfig);

            Assert.That(eventHandler.ClientId, Is.Null);
            Assert.That(eventHandler.IsJoiningApprovalRejected, Is.True);
        });

        [UnityTest]
        public IEnumerator LeaveSuccess() => UniTask.ToCoroutine(async () =>
        {
            var joiningConfig = new MessagingJoiningConfig("MessagingTest");
            await queuingMessagingClient.JoinAsync(joiningConfig);

            Assert.That(eventHandler.LeavingReason, Is.Null);

            await queuingMessagingClient.LeaveAsync();

            Assert.That(eventHandler.LeavingReason, Is.EqualTo("leave request"));
        });

        [Test]
        public void UnexpectedLeft()
        {
            Assert.That(eventHandler.UnexpectedLeftReason, Is.Null);
            messagingClient.FireOnUnexpectedLeft();
            Assert.That(eventHandler.UnexpectedLeftReason, Is.EqualTo("unknown"));
        }

        [Test]
        public void ClientJoined()
        {
            Assert.That(eventHandler.JoinedClientId, Is.Null);
            Assert.That(queuingMessagingClient.JoinedClients.Count, Is.Zero);
            messagingClient.FireOnClientJoined();
            Assert.That(eventHandler.JoinedClientId, Is.EqualTo(otherClientId));
            Assert.That(queuingMessagingClient.JoinedClients.Count, Is.EqualTo(1));
            Assert.That(queuingMessagingClient.JoinedClients[0], Is.EqualTo(eventHandler.JoinedClientId));
        }

        [Test]
        public void ClientLeaving()
        {
            messagingClient.FireOnClientJoined();
            Assert.That(queuingMessagingClient.JoinedClients.Count, Is.EqualTo(1));

            Assert.That(eventHandler.LeavingClientId, Is.Null);
            messagingClient.FireOnClientLeaving();
            Assert.That(eventHandler.LeavingClientId, Is.EqualTo(otherClientId));
            Assert.That(queuingMessagingClient.JoinedClients.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EnqueueRequestSuccess() => UniTask.ToCoroutine(async () =>
        {
            var joiningConfig = new MessagingJoiningConfig("MessagingTest");
            await queuingMessagingClient.JoinAsync(joiningConfig);

            const string message = "TestMessage";
            queuingMessagingClient.EnqueueRequest(message);

            await AssertLogAppearsInSomeFramesAsync($"{nameof(messagingClient.SendMessageAsync)}: message={message}", LogType.Log);
        });

        [Test]
        public void EnqueueRequestWithMessageNull()
            => Assert.That(() => queuingMessagingClient.EnqueueRequest(null),
                Throws.TypeOf<ArgumentNullException>()
                    .With.Message.Contains("message"));

        [Test]
        public void ResponseQueueCountSuccess()
        {
            const string message = "TestMessage";

            var responseQueueCountBeforeReceiving = queuingMessagingClient.ResponseQueueCount();
            Assert.That(responseQueueCountBeforeReceiving, Is.Zero);

            messagingClient.FireOnMessageReceived(message);

            var responseQueueCountAfterReceiving = queuingMessagingClient.ResponseQueueCount();
            Assert.That(responseQueueCountAfterReceiving, Is.EqualTo(1));
        }

        [Test]
        public void DequeueResponseSuccess()
        {
            const string message = "TestMessage";

            messagingClient.FireOnMessageReceived(message);

            (var from, var receivedMessage) = queuingMessagingClient.DequeueResponse();
            Assert.That(from, Is.EqualTo(otherClientId));
            Assert.That(receivedMessage, Is.EqualTo(message));
        }

        private static async UniTask AssertLogAppearsInSomeFramesAsync(string logFragment, LogType logType, int frames = 10)
        {
            var logMessages = new Queue<string>();
            Application.LogCallback logMessageReceivedHandler = (string condition, string stackTrace, LogType type) =>
            {
                if (type == logType)
                {
                    logMessages.Enqueue(condition);
                }
            };
            Application.logMessageReceived += logMessageReceivedHandler;

            for (var i = 0; i < frames; i++)
            {
                while (logMessages.Count > 0)
                {
                    var logMessage = logMessages.Dequeue();
                    if (logMessage.Contains(logFragment))
                    {
                        Application.logMessageReceived -= logMessageReceivedHandler;
                        return;
                    }
                }
                await UniTask.Yield();
            }
            Assert.Fail();
        }

        private class EventHandler
        {
            public string ClientId { get; private set; }
            public void SetClientId(string clientId)
                => ClientId = clientId;

            public string LeavingReason { get; private set; }
            public void SetLeavingReason(string reason)
                => LeavingReason = reason;

            public string UnexpectedLeftReason { get; private set; }
            public void SetUnexpectedLeftReason(string reason)
                => UnexpectedLeftReason = reason;

            public bool IsJoiningApprovalRejected { get; private set; }
            public void SetIsJoiningApprovalRejected(bool isJoiningApprovalRejected)
                => IsJoiningApprovalRejected = isJoiningApprovalRejected;

            public string JoinedClientId { get; private set; }
            public void SetJoinedClientId(string clientId)
                => JoinedClientId = clientId;

            public string LeavingClientId { get; private set; }
            public void SetLeavingClientId(string clientId)
                => LeavingClientId = clientId;

            public string ReceivedMessageFrom { get; private set; }
            public string ReceivedMessage { get; private set; }
            public void SetReceivedMessageInfo((string from, string message) values)
            {
                ReceivedMessageFrom = values.from;
                ReceivedMessage = values.message;
            }

            public void Clear()
            {
                SetClientId(default);
                SetLeavingReason(default);
                SetUnexpectedLeftReason(default);
                SetIsJoiningApprovalRejected(default);
                SetJoinedClientId(default);
                SetLeavingClientId(default);
                SetReceivedMessageInfo(default);
            }
        }
    }
}
