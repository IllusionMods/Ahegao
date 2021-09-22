using BepInEx;

namespace KK_Ahegao
{
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public partial class AhegaoPlugin : BaseUnityPlugin
    {
        public const string Name = "KKS_Ahegao";
        public const string GUID = "KKS_Ahegao";
    }
}