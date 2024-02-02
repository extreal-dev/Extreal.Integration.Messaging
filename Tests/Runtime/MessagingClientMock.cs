using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Extreal.Core.Logging;

namespace Extreal.Integration.Messaging.Test
{
    public class MessagingClientMock : MessagingClient
    {
        private readonly string localClientId = nameof(localClientId);
        private readonly string otherClientId = nameof(otherClientId);

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(MessagingClientMock));

        protected override UniTask<GroupListResponse> DoListGroupsAsync()
        {
            var groups = new List<GroupResponse> {
                new GroupResponse {
                    Name = "TestName",
                }
            };
            var groupListResponse = new GroupListResponse
            {
                Groups = groups,
            };

            return UniTask.FromResult(groupListResponse);
        }

        protected override UniTask DoJoinAsync(MessagingJoiningConfig connectionConfig)
        {
            if (connectionConfig.GroupName == "JoiningApprovalReject")
            {
                FireOnJoiningApprovalRejected();
            }
            else
            {
                FireOnJoined(localClientId);
            }
            return UniTask.CompletedTask;
        }

        protected override UniTask DoLeaveAsync() => UniTask.CompletedTask;

        protected override UniTask DoSendMessageAsync(string message, string to)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(SendMessageAsync)}: message={message}");
            }
            return UniTask.CompletedTask;
        }

        public void FireOnUnexpectedLeft()
            => FireOnUnexpectedLeft("unknown");

        public void FireOnClientJoined()
            => FireOnClientJoined(otherClientId);

        public void FireOnClientLeaving()
            => FireOnClientLeaving(otherClientId);

        public void FireOnMessageReceived(string message)
            => FireOnMessageReceived(otherClientId, message);
    }
}
