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

        private readonly string localUserId = nameof(localUserId);
        private readonly string otherUserId = nameof(otherUserId);

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
                .Subscribe(eventHandler.SetUserId)
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

            queuingMessagingClient.OnUserJoined
                .Subscribe(eventHandler.SetJoinedUserId)
                .AddTo(disposables);

            queuingMessagingClient.OnUserLeaving
                .Subscribe(eventHandler.SetLeavingUserId)
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
            Assert.That(groups[0].Id, Is.EqualTo("TestId"));
            Assert.That(groups[0].Name, Is.EqualTo("TestName"));
        });

        [UnityTest]
        public IEnumerator CreateGroupSuccess() => UniTask.ToCoroutine(async () =>
        {
            const string groupName = "TestGroup";
            var groupConfig = new GroupConfig(groupName);
            await queuingMessagingClient.CreateGroupAsync(groupConfig);
            LogAssert.Expect(LogType.Log, new Regex($".*Group is created: groupName={groupConfig.GroupName}.*"));
        });

        [UnityTest]
        public IEnumerator CreateGroupWithAlreadyExistedGroupName() => UniTask.ToCoroutine(async () =>
        {
            const string groupName = "AlreadyExistedGroupName";
            var groupConfig = new GroupConfig(groupName);

            var exception = default(Exception);
            try
            {
                await queuingMessagingClient.CreateGroupAsync(groupConfig);
            }
            catch (Exception e)
            {
                exception = e;
            }

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.GetType(), Is.EqualTo(typeof(GroupNameAlreadyExistsException)));
            Assert.That(exception.Message, Does.Contain("Group already exists"));
        });

        [UnityTest]
        public IEnumerator DeleteGroupSuccess() => UniTask.ToCoroutine(async () =>
        {
            const string groupName = "TestGroup";
            await queuingMessagingClient.DeleteGroupAsync(groupName);
            LogAssert.Expect(LogType.Log, new Regex($".*{nameof(queuingMessagingClient.DeleteGroupAsync)}: groupName={groupName}.*"));
        });

        [UnityTest]
        public IEnumerator JoinSuccess() => UniTask.ToCoroutine(async () =>
        {
            var joiningConfig = new MessagingJoiningConfig("MessagingTest");

            Assert.That(eventHandler.UserId, Is.Null);
            Assert.That(queuingMessagingClient.IsJoinedGroup, Is.False);

            await queuingMessagingClient.JoinAsync(joiningConfig);

            Assert.That(eventHandler.UserId, Is.EqualTo(localUserId));
            Assert.That(queuingMessagingClient.IsJoinedGroup, Is.True);
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

            Assert.That(eventHandler.UserId, Is.Null);
            Assert.That(queuingMessagingClient.IsJoinedGroup, Is.False);
            Assert.That(eventHandler.IsJoiningApprovalRejected, Is.False);

            await queuingMessagingClient.JoinAsync(joiningConfig);

            Assert.That(eventHandler.UserId, Is.Null);
            Assert.That(queuingMessagingClient.IsJoinedGroup, Is.False);
            Assert.That(eventHandler.IsJoiningApprovalRejected, Is.True);
        });

        [UnityTest]
        public IEnumerator LeaveSuccess() => UniTask.ToCoroutine(async () =>
        {
            var joiningConfig = new MessagingJoiningConfig("MessagingTest");
            await queuingMessagingClient.JoinAsync(joiningConfig);

            Assert.That(eventHandler.LeavingReason, Is.Null);
            Assert.That(queuingMessagingClient.IsJoinedGroup, Is.True);

            await queuingMessagingClient.LeaveAsync();

            Assert.That(eventHandler.LeavingReason, Is.EqualTo("leave request"));
            Assert.That(queuingMessagingClient.IsJoinedGroup, Is.False);
        });

        [Test]
        public void UnexpectedLeft()
        {
            Assert.That(eventHandler.UnexpectedLeftReason, Is.Null);
            messagingClient.FireOnUnexpectedLeft();
            Assert.That(eventHandler.UnexpectedLeftReason, Is.EqualTo("unknown"));
        }

        [Test]
        public void UserJoined()
        {
            Assert.That(eventHandler.JoinedUserId, Is.Null);
            Assert.That(queuingMessagingClient.JoinedUsers.Count, Is.Zero);
            messagingClient.FireOnUserJoined();
            Assert.That(eventHandler.JoinedUserId, Is.EqualTo(otherUserId));
            Assert.That(queuingMessagingClient.JoinedUsers.Count, Is.EqualTo(1));
            Assert.That(queuingMessagingClient.JoinedUsers[0], Is.EqualTo(eventHandler.JoinedUserId));
        }

        [Test]
        public void UserLeaving()
        {
            messagingClient.FireOnUserJoined();
            Assert.That(queuingMessagingClient.JoinedUsers.Count, Is.EqualTo(1));

            Assert.That(eventHandler.LeavingUserId, Is.Null);
            messagingClient.FireOnUserLeaving();
            Assert.That(eventHandler.LeavingUserId, Is.EqualTo(otherUserId));
            Assert.That(queuingMessagingClient.JoinedUsers.Count, Is.Zero);
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
            Assert.That(from, Is.EqualTo(otherUserId));
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
            public string UserId { get; private set; }
            public void SetUserId(string userId)
                => UserId = userId;

            public string LeavingReason { get; private set; }
            public void SetLeavingReason(string reason)
                => LeavingReason = reason;

            public string UnexpectedLeftReason { get; private set; }
            public void SetUnexpectedLeftReason(string reason)
                => UnexpectedLeftReason = reason;

            public bool IsJoiningApprovalRejected { get; private set; }
            public void SetIsJoiningApprovalRejected(bool isJoiningApprovalRejected)
                => IsJoiningApprovalRejected = isJoiningApprovalRejected;

            public string JoinedUserId { get; private set; }
            public void SetJoinedUserId(string userId)
                => JoinedUserId = userId;

            public string LeavingUserId { get; private set; }
            public void SetLeavingUserId(string userId)
                => LeavingUserId = userId;

            public string ReceivedMessageFrom { get; private set; }
            public string ReceivedMessage { get; private set; }
            public void SetReceivedMessageInfo((string from, string message) values)
            {
                ReceivedMessageFrom = values.from;
                ReceivedMessage = values.message;
            }

            public void Clear()
            {
                SetUserId(default);
                SetLeavingReason(default);
                SetUnexpectedLeftReason(default);
                SetIsJoiningApprovalRejected(default);
                SetJoinedUserId(default);
                SetLeavingUserId(default);
                SetReceivedMessageInfo(default);
            }
        }
    }
}
