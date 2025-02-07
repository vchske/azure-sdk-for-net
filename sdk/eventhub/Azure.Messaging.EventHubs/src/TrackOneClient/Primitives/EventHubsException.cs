﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace TrackOne
{
    /// <summary>
    /// Base Exception for various Event Hubs errors.
    /// </summary>
    internal class EventHubsException : Exception
    {
        /// <summary>
        /// Returns a new EventHubsException
        /// </summary>
        /// <param name="isTransient">Specifies whether or not the exception is transient.</param>
        public EventHubsException(bool isTransient)
        {
            IsTransient = isTransient;
        }

        /// <summary>
        /// Returns a new EventHubsException
        /// </summary>
        /// <param name="isTransient">Specifies whether or not the exception is transient.</param>
        /// <param name="message">The detailed message exception.</param>
        public EventHubsException(bool isTransient, string message)
            : base(message)
        {
            IsTransient = isTransient;
        }

        /// <summary>
        /// Returns a new EventHubsException
        /// </summary>
        /// <param name="isTransient">Specifies whether or not the exception is transient.</param>
        /// <param name="innerException">The inner exception.</param>
        public EventHubsException(bool isTransient, Exception innerException)
            : base(innerException.Message, innerException)
        {
            IsTransient = isTransient;
        }

        /// <summary>
        /// Returns a new EventHubsException
        /// </summary>
        /// <param name="isTransient">Specifies whether or not the exception is transient.</param>
        /// <param name="message">The detailed message exception.</param>
        /// <param name="innerException">The inner exception.</param>
        public EventHubsException(bool isTransient, string message, Exception innerException)
            : base(message, innerException)
        {
            IsTransient = isTransient;
        }

        /// <summary>
        /// Gets the message as a formatted string.
        /// </summary>
        public override string Message
        {
            get
            {
                string baseMessage = base.Message;
                if (string.IsNullOrEmpty(EventHubsNamespace))
                {
                    return baseMessage;
                }

                return "{0}, ({1})".FormatInvariant(base.Message, EventHubsNamespace);
            }
        }

        public string RawMessage => base.Message;

        /// <summary>
        /// A boolean indicating if the exception is a transient error or not.
        /// </summary>
        /// <value>returns true when user can retry the operation that generated the exception without additional intervention.</value>
        public bool IsTransient { get; }

        /// <summary>
        /// Gets the Event Hubs namespace from which the exception occurred, if available.
        /// </summary>
        public string EventHubsNamespace { get; internal set; }
    }
}
