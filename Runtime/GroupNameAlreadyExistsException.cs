using System;

namespace Extreal.Integration.Messaging
{
    /// <summary>
    /// Exception thrown when group name already exists at creation of group.
    /// </summary>
    public class GroupNameAlreadyExistsException : Exception
    {
        /// <summary>
        /// Creates a new exception.
        /// </summary>
        /// <param name="message">Message.</param>
        public GroupNameAlreadyExistsException(string message) : base(message)
        {
        }
    }
}
