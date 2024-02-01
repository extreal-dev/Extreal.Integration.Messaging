using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UniRx;

namespace Extreal.Integration.Messaging
{
    /// <summary>
    /// Class for group messaging.
    /// </summary>
    public abstract class MessagingClient : DisposableBase
    {
        /// <summary>
        /// IDs of joined clients.
        /// </summary>
        public IReadOnlyList<string> JoinedClients => joinedClients;
        private readonly List<string> joinedClients = new List<string>();

        /// <summary>
        /// <para>Invokes immediately after this client joined a group.</para>
        /// Arg: Client ID of this client.
        /// </summary>
        public IObservable<string> OnJoined => onJoined;
        private readonly Subject<string> onJoined;
        protected void FireOnJoined(string clientId) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnJoined)}: clientId={clientId}");
            }
            onJoined.OnNext(clientId);
        });

        /// <summary>
        /// <para>Invokes just before this client leaves a group.</para>
        /// Arg: reason why this client leaves.
        /// </summary>
        public IObservable<string> OnLeaving => onLeaving;
        private readonly Subject<string> onLeaving;
        protected void FireOnLeaving(string reason) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnLeaving)}: reason={reason}");
            }
            onLeaving.OnNext(reason);
        });

        /// <summary>
        /// <para>Invokes immediately after this client unexpectedly leaves a group.</para>
        /// Arg: reason why this client leaves.
        /// </summary>
        public IObservable<string> OnUnexpectedLeft => onUnexpectedLeft;
        private readonly Subject<string> onUnexpectedLeft;
        protected void FireOnUnexpectedLeft(string reason) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUnexpectedLeft)}: reason={reason}");
            }
            onUnexpectedLeft.OnNext(reason);
        });

        /// <summary>
        /// Invokes immediately after the joining approval is rejected.
        /// </summary>
        public IObservable<Unit> OnJoiningApprovalRejected => onJoiningApprovalRejected;
        private readonly Subject<Unit> onJoiningApprovalRejected;
        protected void FireOnJoiningApprovalRejected() => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnJoiningApprovalRejected)}");
            }
            onJoiningApprovalRejected.OnNext(Unit.Default);
        });

        /// <summary>
        /// <para>Invokes immediately after a client joined the same group this client joined.</para>
        /// Arg: ID of the joined client.
        /// </summary>
        public IObservable<string> OnClientJoined => onClientJoined;
        private readonly Subject<string> onClientJoined;
        protected void FireOnClientJoined(string clientId) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnClientJoined)}: clientId={clientId}");
            }
            onClientJoined.OnNext(clientId);
        });

        /// <summary>
        /// <para>Invokes just before a client leaves the group this client joined.</para>
        /// Arg: ID of the left client.
        /// </summary>
        public IObservable<string> OnClientLeaving => onClientLeaving;
        private readonly Subject<string> onClientLeaving;
        protected void FireOnClientLeaving(string clientId) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnClientLeaving)}: clientId={clientId}");
            }
            onClientLeaving.OnNext(clientId);
        });

        /// <summary>
        /// <para>Invokes immediately after the message is received.</para>
        /// Arg: ID of the client sending the message and the message.
        /// </summary>
        public IObservable<(string clientId, string message)> OnMessageReceived => onMessageReceived;
        private readonly Subject<(string, string)> onMessageReceived;
        protected void FireOnMessageReceived(string clientId, string message) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnMessageReceived)}: clientId={clientId}, message={message}");
            }
            onMessageReceived.OnNext((clientId, message));
        });

        private bool isJoined;

        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(MessagingClient));

        /// <summary>
        /// Creates a new MessagingClient.
        /// </summary>
        [SuppressMessage("Usage", "CC0022")]
        protected MessagingClient()
        {
            onJoined = new Subject<string>().AddTo(disposables);
            onLeaving = new Subject<string>().AddTo(disposables);
            onUnexpectedLeft = new Subject<string>().AddTo(disposables);
            onClientJoined = new Subject<string>().AddTo(disposables);
            onClientLeaving = new Subject<string>().AddTo(disposables);
            onJoiningApprovalRejected = new Subject<Unit>().AddTo(disposables);
            onMessageReceived = new Subject<(string, string)>().AddTo(disposables);

            OnJoined
                .Subscribe(_ => isJoined = true)
                .AddTo(disposables);

            OnLeaving
                .Merge(OnUnexpectedLeft)
                .Subscribe(_ => isJoined = false)
                .AddTo(disposables);

            OnClientJoined
                .Subscribe(joinedClients.Add)
                .AddTo(disposables);

            OnClientLeaving
                .Subscribe(clientId => joinedClients.Remove(clientId))
                .AddTo(disposables);
        }

        protected sealed override void ReleaseManagedResources()
        {
            disposables.Dispose();
            DoReleaseManagedResources();
        }

        /// <summary>
        /// Releases managed resources in sub class.
        /// </summary>
        protected virtual void DoReleaseManagedResources() { }

        /// <summary>
        /// Lists groups that currently exist.
        /// </summary>
        /// <returns>List of the groups that currently exist.</returns>
        public async UniTask<List<Group>> ListGroupsAsync()
        {
            var groupList = await DoListGroupsAsync();
            return groupList.Groups.Select(groupResponse => new Group(groupResponse.Id, groupResponse.Name)).ToList();
        }

        /// <summary>
        /// Lists groups that currently exist in sub class.
        /// </summary>
        /// <returns>List of the groups that currently exist.</returns>
        protected abstract UniTask<GroupListResponse> DoListGroupsAsync();

        /// <summary>
        /// Joins a group.
        /// </summary>
        /// <param name="joiningConfig">Joining Config.</param>
        public UniTask JoinAsync(MessagingJoiningConfig joiningConfig)
        {
            if (joiningConfig == null)
            {
                throw new ArgumentNullException(nameof(joiningConfig));
            }

            return DoJoinAsync(joiningConfig);
        }

        /// <summary>
        /// Joins a group in sub class.
        /// </summary>
        /// <param name="joiningConfig">Joining Config.</param>
        protected abstract UniTask DoJoinAsync(MessagingJoiningConfig joiningConfig);

        /// <summary>
        /// Leaves a group.
        /// </summary>
        public UniTask LeaveAsync()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug(nameof(LeaveAsync));
            }
            FireOnLeaving("leave request");
            return DoLeaveAsync();
        }

        /// <summary>
        /// Leaves a group in sub class.
        /// </summary>
        /// <remarks>
        /// OnLeaving event with "leave request" parameter is fired on super class.
        /// </remarks>
        protected abstract UniTask DoLeaveAsync();

        /// <summary>
        /// Sends a message.
        /// </summary>
        /// <param name="message">Message to be sent.</param>
        /// <param name="to">
        ///     Client ID of the destination.
        ///     <para>Sends a message to the entire group if not specified.</para>
        /// </param>
        /// <exception cref="ArgumentNullException">When message is null.</exception>
        public async UniTask SendMessageAsync(string message, string to = default)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!isJoined)
            {
                if (Logger.IsWarn())
                {
                    Logger.LogWarn("Called Send method before joining a group");
                }
                return;
            }

            await DoSendMessageAsync(message, to);
        }

        /// <summary>
        /// Sends a message in sub class.
        /// </summary>
        /// <param name="message">Message to be sent.</param>
        /// <param name="to">
        ///     Client ID of the destination.
        ///     <para>Sends a message to the entire group if value is default.</para>
        /// </param>
        /// <exception cref="ArgumentNullException">When message is null.</exception>
        protected abstract UniTask DoSendMessageAsync(string message, string to);
    }
}
