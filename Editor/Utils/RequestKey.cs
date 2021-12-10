using System;

namespace UnityEditor.XR.ObjectCapture
{
    readonly struct RequestKey : IEquatable<RequestKey>
    {
        readonly Guid m_SessionId;
        readonly Guid m_RequestId;

        public RequestKey(Guid sessionId, Guid requestId)
        {
            m_SessionId = sessionId;
            m_RequestId = requestId;
        }

        public bool Equals(RequestKey other)
        {
            return m_SessionId.Equals(other.m_SessionId) && m_RequestId.Equals(other.m_RequestId);
        }

        public override bool Equals(object obj)
        {
            return obj is RequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (m_SessionId.GetHashCode() * 397) ^ m_RequestId.GetHashCode();
            }
        }
    }
}
