using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using System;
using System.Collections.Generic;
using UniRx;

namespace Extreal.Integration.Messaging
{
    /// <summary>
    /// Class that wraps MessagingClient so that it is possible to control the timing of handling messages.
    /// </summary>
    public class QueuingMessagingClient : DisposableBase
    {
        /// <summary>
        /// Whether this client has joined a group or not.
        /// </summary>
        /// <value>True if joined, false otherwise.</value>
        public bool IsJoinedGroup => messagingClient.IsJoinedGroup;

        /// <summary>
        /// IDs of joined clients.
        /// </summary>
        public IReadOnlyList<string> JoinedClients => messagingClient.JoinedClients;

        /// <summary>
        /// <para>Invokes immediately after this client joined a group.</para>
        /// Arg: Client ID of this client.
        /// </summary>
        public IObservable<string> OnJoined => messagingClient.OnJoined;

        /// <summary>
        /// <para>Invokes just before this client leaves a group.</para>
        /// Arg: reason why this client leaves.
        /// </summary>
        public IObservable<string> OnLeaving => messagingClient.OnLeaving;

        /// <summary>
        /// <para>Invokes immediately after this client unexpectedly leaves a group.</para>
        /// Arg: reason why this client leaves.
        /// </summary>
        public IObservable<string> OnUnexpectedLeft => messagingClient.OnUnexpectedLeft;

        /// <summary>
        /// Invokes immediately after the joining approval is rejected.
        /// </summary>
        public IObservable<Unit> OnJoiningApprovalRejected => messagingClient.OnJoiningApprovalRejected;

        /// <summary>
        /// <para>Invokes immediately after a client joins the group this client joined.</para>
        /// Arg: ID of the joined client.
        /// </summary>
        public IObservable<string> OnClientJoined => messagingClient.OnClientJoined;

        /// <summary>
        /// <para>Invokes just before a client leaves the group this client joined.</para>
        /// Arg: ID of the left client.
        /// </summary>
        public IObservable<string> OnClientLeaving => messagingClient.OnClientLeaving;

        private readonly MessagingClient messagingClient;

        private readonly Queue<(string, string)> requestQueue = new Queue<(string, string)>();
        private readonly Queue<(string, string)> responseQueue = new Queue<(string, string)>();

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        /// <summary>
        /// Creates a new QueuingMessagingClient.
        /// </summary>
        /// <param name="messagingClient">MessagingClient.</param>
        /// <exception cref="ArgumentNullException">When messagingClient is null.</exception>
        public QueuingMessagingClient(MessagingClient messagingClient)
        {
            if (messagingClient == null)
            {
                throw new ArgumentNullException(nameof(messagingClient));
            }

            this.messagingClient = messagingClient.AddTo(disposables);

            messagingClient.OnMessageReceived
                .Subscribe(responseQueue.Enqueue)
                .AddTo(disposables);

            Observable.EveryUpdate()
                .Subscribe(_ => UpdateAsync().Forget())
                .AddTo(disposables);
        }

        protected override void ReleaseManagedResources()
            => disposables.Dispose();

        private async UniTaskVoid UpdateAsync()
        {
            while (requestQueue.Count > 0)
            {
                (var to, var message) = requestQueue.Dequeue();
                if (IsJoinedGroup)
                {
                    await messagingClient.SendMessageAsync(message, to);
                }
            }
        }

        /// <summary>
        /// Enqueues a message to request queue.
        /// </summary>
        /// <param name="message">Message to be sent.</param>
        /// <param name="to">
        ///     Client ID of the destination.
        ///     <para>Sends a message to the entire group if not specified.</para>
        /// </param>
        public void EnqueueRequest(string message, string to = default)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException(nameof(message));
            }

            requestQueue.Enqueue((to, message));
        }

        /// <summary>
        /// Counts the number of elements in the response queue.
        /// </summary>
        /// <returns>Number of elements in the response queue.</returns>
        public int ResponseQueueCount()
            => responseQueue.Count;

        /// <summary>
        /// Dequeues from response queue.
        /// </summary>
        /// <returns>ID of the client sending the message and the message.</returns>
        public (string from, string message) DequeueResponse()
            => responseQueue.Dequeue();

        /// <summary>
        /// Lists groups that currently exist.
        /// </summary>
        /// <returns>List of the groups that currently exist.</returns>
        public UniTask<List<Group>> ListGroupsAsync()
            => messagingClient.ListGroupsAsync();

        /// <summary>
        /// Joins a group.
        /// </summary>
        /// <param name="joiningConfig">Joining Config.</param>
        public UniTask JoinAsync(MessagingJoiningConfig joiningConfig)
            => messagingClient.JoinAsync(joiningConfig);

        /// <summary>
        /// Leaves a group.
        /// </summary>
        public UniTask LeaveAsync()
            => messagingClient.LeaveAsync();
    }
}
