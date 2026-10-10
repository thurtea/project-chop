using FishNet.Broadcast;

namespace Chopper.Networking
{
    // Sent by every client right after it connects, before it is let in (JoinApproval). The host's own client sends
    // the host token, which only that process knows, and is let in at once.
    public struct JoinRequestBroadcast : IBroadcast
    {
        public string Name;
        public string HostToken;
    }

    // Sent by the host to a player it turned away (or did not answer), just before closing the connection.
    public struct JoinDeniedBroadcast : IBroadcast
    {
        public string Reason;
    }
}
