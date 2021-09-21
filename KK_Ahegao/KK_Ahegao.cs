using BepInEx;

namespace KK_Ahegao
{
    [BepInProcess("Koikatu"), BepInProcess("Koikatsu Party"), BepInProcess("KoikatuVR"),
     BepInProcess("Koikatsu Party VR")]
    partial class KK_Ahegao
    {
        public const string Name = "KK_Ahegao";
        public const string GUID = "KK_Ahegao";
    }
}