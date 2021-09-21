using BepInEx;

namespace KK_Ahegao
{
    [BepInProcess("Koikatu")]
    [BepInProcess("Koikatsu Party")]
    [BepInProcess("KoikatuVR")]
    [BepInProcess("Koikatsu Party VR")]
    public partial class AhegaoPlugin : BaseUnityPlugin
    {
        public const string Name = "KK_Ahegao";
        public const string GUID = "KK_Ahegao";
    }
}