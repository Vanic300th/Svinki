#if !UNITY_WEBGL && !EOS_DISABLE
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.P2P;
using Epic.OnlineServices.Platform;
namespace FishNet.Plugins.FishyEOS.Util
{
    public static class EOS
    {
        public static PlatformInterface Platform { get; set; }
        public static ProductUserId LocalProductUserId => Platform?.GetConnectInterface().GetLoggedInUserByIndex(0);
        public static void ClearCachedInterface() { }
        public static PlatformInterface GetPlatformInterface() => Platform;
        public static ConnectInterface GetCachedConnectInterface() => Platform?.GetConnectInterface();
        public static P2PInterface GetCachedP2PInterface() => Platform?.GetP2PInterface();
    }
}
#endif
