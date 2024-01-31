using System;

namespace Extreal.Integration.Messaging
{
    /// <summary>
    /// Class that holds joining config for messaging.
    /// </summary>
    public class MessagingJoiningConfig
    {
        /// <summary>
        /// Group name.
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// Creates a new GroupConfig.
        /// </summary>
        /// <param name="groupName">Group name.</param>
        /// <exception cref="ArgumentNullException">When groupName is null.</exception>
        public MessagingJoiningConfig(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            GroupName = groupName;
        }
    }
}
