using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System.Collections;
using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using KeyboardShortcut = BepInEx.Configuration.KeyboardShortcut;

namespace KK_Ahegao {
    [BepInPlugin(Name, GUID, Version)]
    [BepInProcess("Koikatu"), BepInProcess("Koikatsu Party")]
    class KK_Ahegao : BaseUnityPlugin {

        public const string Name = "KK_Ahegao";
        public const string GUID = "KK_Ahegao";
        public const string Version = "1.11";


        #region Config properties      
        public static ConfigEntry<uint> AhegaoCount { get; private set; }
        public static ConfigEntry<bool> cfgSetEyeBrows { get; private set; }
        public static ConfigEntry<bool> cfgSetEyes { get; private set; }
        public static ConfigEntry<bool> cfgSetMouth { get; private set; }
        public static ConfigEntry<bool> cfgSpeedToggle { get; private set; }
        public static ConfigEntry<int> cfgAhegaoEyebrow { get; private set; }
        public static ConfigEntry<int> cfgAhegaoEyes { get; private set; }
        public static ConfigEntry<int> cfgAhegaoMouth { get; private set; }
        public static ConfigEntry<bool> cfgRollEnabled { get; private set; }
        public static ConfigEntry<float> cfgEyeY { get; private set; }
        public static ConfigEntry<bool> cfgSetTears { get; private set; }
        public static ConfigEntry<bool> cfgSetBlush { get; private set; }
        public static ConfigEntry<byte> cfgAhegaoTears { get; private set; }
        public static ConfigEntry<float> cfgAhegaoBlush { get; private set; }
        public static ConfigEntry<bool> cfgDarkEnabled { get; private set; }
        public static ConfigEntry<KeyboardShortcut> AhegaoHotkey { get; private set; }
        //public static ConfigEntry<KeyboardShortcut> ReloadConfigHotkey { get; private set; }
        #endregion

        private static uint OrgasmCount = 0;
        private static bool inHScene = false;

        private static Harmony hi = null;
        private static bool hold;
        private static bool fastEnough;
        private static List<ChaControl> lstFemale;
        private static bool isDarkness = false;
        private static string animName;
        private static bool isValidMode = false;
        private static bool isKiss = false;

        private static readonly Vector2 originalOffset = new Vector2(-1f, -0.8f);
        private static Vector2 newOffset;

        // private List<HActionBase> lstProc = null;

        public KK_Ahegao() {
            SceneManager.sceneLoaded += SceneLoaded;

            //ConvertConfig();

            var detectDark = typeof(ChaInfo).GetProperty("exType", BindingFlags.Public | BindingFlags.Instance) != null;

            var s = "Settings";

            AhegaoCount = Config.Bind<uint>(s, "Ahegao Count", 3, "How many orgasms before ahegao.");


            cfgSetEyeBrows = Config.Bind(s, "Set Eyebrows", true, "Whether eyebrows will be changed during ahegao.");
            cfgSetEyes = Config.Bind(s, "Set Eyes", true, "Whether eyes will be changed during ahegao.");
            cfgSetMouth = Config.Bind(s, "Set Mouth", true, "Whether the mouth will be changed during ahegao.");
            cfgSpeedToggle = Config.Bind(s, "Speed Toggle", true, "When enabled, ahegao only happens above 50% speed.");

            cfgAhegaoEyebrow = Config.Bind(s, "Ahegao Eyebrow ID", 2, "ID of the eyebrow expression to set during ahegao.");
            cfgAhegaoEyes = Config.Bind(s, "Ahegao Eye ID", 25, "ID of the eye expression to set during ahegao.");
            cfgAhegaoMouth = Config.Bind(s, "Ahegao Mouth ID", 24, "ID of the mouth expression to set during ahegao.");

            cfgRollEnabled = Config.Bind(s, "Eye Rolling", true, "When enabled, the eyes will roll back during the ahegao state.");
            cfgEyeY = Config.Bind(s, "Eye Roll Amount", 0.25f, new ConfigDescription("How much the eyes should roll.", new AcceptableValueRange<float>(0, 0.5f)));
            newOffset = new Vector2(originalOffset.x, originalOffset.y - (cfgEyeY.Value * 2f));
            cfgEyeY.SettingChanged += (object sender, EventArgs args) => { newOffset.y = (originalOffset.y - (cfgEyeY.Value * 2f)); };


            cfgSetTears = Config.Bind(s, "Set Tears", true, "Whether tears will be displayed during ahegao or not.");
            cfgSetBlush = Config.Bind(s, "Set Blush", true, "Whether the custom blush amount will be displayed during ahegao.");
            cfgAhegaoTears = Config.Bind<byte>(s, "Tears Level", 0, new ConfigDescription("The level of tears to display during ahegao.\n0 is none.", new AcceptableValueList<byte>(new byte[] { 0, 1, 2, 3 })));
            cfgAhegaoBlush = Config.Bind(s, "Blush Level", 0f, new ConfigDescription("The level of blush displayed during ahegao.\n0 for none.", new AcceptableValueRange<float>(0f, 1f)));

            AhegaoHotkey = Config.Bind("", "Reset Ahegao Hotkey", new KeyboardShortcut(KeyCode.O), "Resets the orgasm count to zero.");           

            if (detectDark) {
                cfgDarkEnabled = Config.Bind(s, "Darkness Toggle", true, "Whether or not ahegao can trigger in the Darkness mode.");
            }

            Config.SettingChanged += (object sender, SettingChangedEventArgs args) => { RefreshFace(); };
        }

        void SceneLoaded(Scene s, LoadSceneMode lsm) {
            var ihs = Singleton<HSceneProc>.Instance != null;
            if (!inHScene && ihs) {
                hold = false;
                fastEnough = false;
                animName = "";
                lstFemale = null;
                StartCoroutine(ResolveLstFemale());
            }
            inHScene = ihs;
        }


        IEnumerator ResolveLstFemale() {
            while (lstFemale == null || lstFemale?.Count == 0) {
                lstFemale = (List<ChaControl>)AccessTools.Field(typeof(HSceneProc), "lstFemale").GetValue(Singleton<HSceneProc>.Instance);
                yield return null;
            }
            // lstProc = (List<HActionBase>)AccessTools.Field(typeof(HSceneProc), "lstProc").GetValue(Singleton<HSceneProc>.Instance);
            ReloadConfig();
            ResetAhegao();    //Reset orgasm counter on H scene entry.                     
            if (hi == null) hi = Harmony.CreateAndPatchAll(typeof(KK_Ahegao));

        }

        private static void RemovePatches() {
            if (hi != null) {
                hi.UnpatchAll(hi.Id);
                hi = null;
            }
        }

        private void ReloadConfig() {
            Config.Reload();
            RefreshFace();
        }


        private static void AddOrgasm() {
            if (!isValidMode) return;
            if (!inHScene) return;
            if (++OrgasmCount >= AhegaoCount.Value) RefreshFace();
        }

        private static void RefreshFace() {
            if (!isValidMode) return;
            foreach (var female in lstFemale) {
                if (female.eyesCtrl.FBSTarget.Any(x => x.GetSkinnedMeshRenderer() == null)) continue;   //Apparently sharedMesh can nullref
                female.ChangeEyebrowPtn(0);
                female.ChangeEyesPtn(0);
                female.ChangeMouthPtn(0);
            }
        }

        private static void ResetAhegao() {
            OrgasmCount = 0;
            RefreshFace();
        }

        void Update() {
            if (!inHScene || lstFemale == null || lstFemale.Count == 0) return;
            if (AhegaoHotkey.Value.IsDown()) ResetAhegao();
            //if (ReloadConfigHotkey.Value.IsDown()) ReloadConfig();

            foreach (var cc in lstFemale) {
                if (cc == null) return;
                var hsp = Singleton<HSceneProc>.Instance;
                if (hsp == null) return;

                if (hsp.hand) {
                    var ik = hsp.hand.IsKissAction();
                    var isKissJustChanged = (!isKiss && ik);
                    isKiss = ik;
                    if (isKissJustChanged) RefreshFace();
                }
                if (isKiss) return;
                if (!inHScene || lstFemale == null || lstFemale.Count == 0) return;
                //Force refresh of face when adjusting speed.
                var fe = hsp.flags.speed > 2f;
                if (fe != fastEnough) {
                    fastEnough = fe;
                    RefreshFace();
                }
                //Allow face state to remain when transitioning to orgasm animation.
                var aci = cc.animBody.GetCurrentAnimatorClipInfo(0);
                if (aci.Length == 0) return;
                var an = aci[0].clip.name;
                if (animName != an) {
                    hold = false;
                    animName = an;
                    string mode = hsp.flags.mode.ToString();
                    isValidMode = mode == "sonyu" || mode == "aibu" || mode == "sonyu3P" || ((mode == "sonyu3PMMF") && (cfgDarkEnabled.Value));
                    isDarkness = mode == "sonyu3PMMF";
                    var hl = an.Contains("Loop1") || an.Contains("Loop2") || an.Contains("OLoop") || an.Contains("IN_Start") || an.Contains("IN_Loop") || an.Contains("_IN_A");
                    var rf = an.Contains("Idle") || an.Contains("OUT_A") || an.Contains("OUT_Start") || an.Contains("OUT_Loop");// || an.Contains("_IN_A") || an.Contains("OUT_Start") || an.Contains("OUT_Loop") || an.Contains("OUT_A");
                    if (hl) hold = true;
                    else if (rf) {
                        if (!(mode == "aibu")) //Ignore caress mode.
                        {
                            hsp.flags.speed = 0f;
                            RefreshFace();
                        }
                    }
                }
            }
        }

        void LateUpdate() {
            if (!inHScene || lstFemale == null || lstFemale.Count == 0) return;
            foreach (var cc in lstFemale) {
                if (cc == null) return;
                var hsp = Singleton<HSceneProc>.Instance;
                if (hsp == null) return;
                Roll(cc, ShouldProc() && !ShouldNotProc(cc) && cfgRollEnabled.Value);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HFlag), "AddAibuOrg")] //Foreplay
        public static void AddAibuOrg() => AddOrgasm();

        [HarmonyPostfix, HarmonyPatch(typeof(HFlag), "AddSonyuOrg")]    //Vaginal
        public static void AddSonyuOrg() => AddOrgasm();

        [HarmonyPostfix, HarmonyPatch(typeof(HFlag), "AddSonyuAnalOrg")]    //Anal
        public static void AddSonyuAnalOrg() => AddOrgasm();

        [HarmonyPrefix, HarmonyPatch(typeof(ChaControl), "ChangeEyebrowPtn")]
        public static void ChangeEyebrowPtn(ChaControl __instance, ref int ptn) {
            if (cfgSetEyeBrows.Value) ChangePtn(ref __instance, ref ptn, cfgAhegaoEyebrow.Value);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ChaControl), "ChangeEyesPtn")]
        public static void ChangeEyesPtn(ChaControl __instance, ref int ptn) {
            if (cfgSetEyes.Value) ChangePtn(ref __instance, ref ptn, cfgAhegaoEyes.Value);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ChaControl), "ChangeMouthPtn")]
        public static void ChangeMouthPtn(ChaControl __instance, ref int ptn) {
            if (cfgSetMouth.Value) ChangePtn(ref __instance, ref ptn, cfgAhegaoMouth.Value);
        }

        private static void ChangePtn(ref ChaControl __instance, ref int ptn, int newPtn) {
            //if we're not in a H-scene or are kissing, do not change the pattern
            if (!isValidMode) return;
            if (isKiss) return;            
            if (!lstFemale.Contains(__instance) || OrgasmCount < AhegaoCount.Value || !inHScene) return;
            if (cfgSpeedToggle.Value && !fastEnough || !hold) {
                //don't set the face to 0 during darkness since that isn't the default
                if (isDarkness) return;
                else ptn = 0;
            } else ptn = newPtn;
          
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HSceneProc), "EndProc")]
        public static void EndProc() {
            if (!inHScene) return;
            inHScene = false;
            RemovePatches();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(FaceListCtrl), "SetFace")]
        public static void SetFace(ChaControl _chara, bool __result) {
            if (!__result) return;
            if (ShouldNotProc(_chara)) return;
            if (!ShouldProc()) return;
            if (cfgSetTears.Value) _chara.tearsLv = cfgAhegaoTears.Value;
            if (cfgSetBlush.Value) _chara.ChangeHohoAkaRate(cfgAhegaoBlush.Value);
        }

        private static bool ShouldNotProc(ChaControl female) => female == null || !isValidMode || OrgasmCount < AhegaoCount.Value || !inHScene || isKiss;

        private static bool ShouldProc() => !(cfgSpeedToggle.Value && !fastEnough || !hold);
        //functionally equivalent to (!cfgSpeedToggle.Value && hold) || (fastEnough && hold)

        private static void Roll(ChaControl target, bool roll) {
            if (roll) {
                foreach (EyeLookMaterialControll eLMC in target.eyeLookMatCtrl) {
                    foreach (EyeLookMaterialControll.TexState tS in eLMC.texStates) {
                        Vector2 textureOffset = eLMC._renderer.material.GetTextureOffset(tS.texID);
                        textureOffset.y -= cfgEyeY.Value;
                        eLMC._renderer.material.SetTextureOffset(tS.texID, textureOffset);

                    }
                }
                // Unlike the above, this offset doesn't reset itself each frame, so we have to make sure it doesn't
                // just shoot off into space. The multiplication by 2 in the newOffset creation is because they seem to be on different scales, and
                // 2 is my best approximation                
                target.rendEye[0].material.SetTextureOffset(ChaShader._expression, newOffset);
                target.rendEye[1].material.SetTextureOffset(ChaShader._expression, newOffset);
            } else {
                // I'm unsure if this part is actually required (I don't think the offset matters if the texture doesn't show
                // after all), but it's probably better to be safe since as I noted before, this offset doesn't get reset autoatically
                target.rendEye[0].material.SetTextureOffset(ChaShader._expression, originalOffset);
                target.rendEye[1].material.SetTextureOffset(ChaShader._expression, originalOffset);
            }
        }
    }
}


