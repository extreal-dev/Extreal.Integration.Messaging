using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Extreal.Core.Logging;

namespace Extreal.Integration.Messaging.Test
{
    public class MessagingClientMock : MessagingClient
    {
        private readonly string localUserId = nameof(localUserId);
        private readonly string otherUserId = nameof(otherUserId);

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(MessagingClientMock));

        protected override UniTask<GroupListResponse> DoListGroupsAsync()
        {
            var groups = new List<GroupResponse> {
                new GroupResponse{
                    Id = "TestId",
                    Name = "TestName",
                }
            };
            var groupListResponse = new GroupListResponse
            {
                Groups = groups,
            };

            return UniTask.FromResult(groupListResponse);
        }

        protected override UniTask<CreateGroupResponse> DoCreateGroupAsync(GroupConfig groupConfig)
        {
            CreateGroupResponse createGroupResponse;
            if (groupConfig.GroupName == "AlreadyExistedGroupName")
            {
                createGroupResponse = new CreateGroupResponse
                {
                    Status = 409,
                    Message = "Group already exists",
                };
            }
            else
            {
                if (Logger.IsDebug())
                {
                    Logger.LogDebug($"Group is created: groupName={groupConfig.GroupName}");
                }
                createGroupResponse = new CreateGroupResponse
                {
                    Status = 200,
                    Message = "Group have been created",
                };
            }
            return UniTask.FromResult(createGroupResponse);
        }

        public override UniTask DeleteGroupAsync(string groupName)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(DeleteGroupAsync)}: groupName={groupName}");
            }
            return UniTask.CompletedTask;
        }

        protected override UniTask DoJoinAsync(MessagingJoiningConfig connectionConfig)
        {
            if (connectionConfig.GroupName == "JoiningApprovalReject")
            {
                FireOnJoiningApprovalRejected();
            }
            else
            {
                SetJoiningGroupStatus(true);
                FireOnJoined(localUserId);
            }
            return UniTask.CompletedTask;
        }

        protected override UniTask DoLeaveAsync()
        {
            SetJoiningGroupStatus(false);
            return UniTask.CompletedTask;
        }

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

        public void FireOnUserJoined()
            => FireOnUserJoined(otherUserId);

        public void FireOnUserLeaving()
            => FireOnUserLeaving(otherUserId);

        public void FireOnMessageReceived(string message)
            => FireOnMessageReceived(otherUserId, message);
    }
}
