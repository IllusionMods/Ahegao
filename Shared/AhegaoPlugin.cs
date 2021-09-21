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
using Object = UnityEngine.Object;

namespace KK_Ahegao
{
    [BepInPlugin(Name, GUID, Version)]
    public partial class AhegaoPlugin : BaseUnityPlugin
    {
        public const string Version = "1.12";

        #region Config properties

        public static ConfigEntry<uint> AhegaoCount { get; private set; }
        public static ConfigEntry<bool> CfgSetEyeBrows { get; private set; }
        public static ConfigEntry<bool> CfgSetEyes { get; private set; }
        public static ConfigEntry<bool> CfgSetMouth { get; private set; }
        public static ConfigEntry<bool> CfgSpeedToggle { get; private set; }
        public static ConfigEntry<int> CfgMinSpeed { get; private set; }
        public static ConfigEntry<int> CfgAhegaoEyebrow { get; private set; }
        public static ConfigEntry<int> CfgAhegaoEyes { get; private set; }
        public static ConfigEntry<int> CfgAhegaoMouth { get; private set; }
        public static ConfigEntry<bool> CfgRollEnabled { get; private set; }
        public static ConfigEntry<float> CfgEyeY { get; private set; }
        public static ConfigEntry<bool> CfgSetTears { get; private set; }
        public static ConfigEntry<bool> CfgSetBlush { get; private set; }
        public static ConfigEntry<byte> CfgAhegaoTears { get; private set; }
        public static ConfigEntry<float> CfgAhegaoBlush { get; private set; }
        public static ConfigEntry<bool> CfgDarkEnabled { get; private set; }

        public static ConfigEntry<KeyboardShortcut> AhegaoHotkey { get; private set; }
        //public static ConfigEntry<KeyboardShortcut> ReloadConfigHotkey { get; private set; }

        #endregion

        private static AhegaoPlugin _instance;

        private static uint _orgasmCount;

        private static Harmony _hi;
        private static bool _hold;
        private static bool _fastEnough;
        private static List<ChaControl> _lstFemale;
        private static bool _isDarkness;
        private static bool _isVr;
        private static string _animName;
        private static bool _isValidMode;
        private static bool _isKiss;

        private static readonly Vector2 _originalOffset = new Vector2(-1f, -0.8f);
        private static Vector2 _newOffset;

        private static Type _hSceneType;
        private static Object _hSceneProc;
        private static HFlag _hflags;
        private static Func<bool> _isKissActionDelegate;

        private void Awake()
        {
            _instance = this;
            // Only enable when inside h scene
            enabled = false;

            SceneManager.sceneLoaded += SceneLoaded;

            var detectDark = typeof(ChaInfo).GetProperty("exType", BindingFlags.Public | BindingFlags.Instance) != null;
            _hSceneType = Type.GetType("VRHScene, Assembly-CSharp");
            _isVr = _hSceneType != null;
            if (!_isVr)
            {
                _hSceneType = Type.GetType("HSceneProc, Assembly-CSharp");
                if (_hSceneType == null) throw new Exception("HSceneProc missing");
            }

            const string s = "Settings";

            AhegaoCount = Config.Bind<uint>(s, "Ahegao Count", 3, "How many orgasms before ahegao.");


            CfgSetEyeBrows = Config.Bind(s, "Set Eyebrows", true, "Whether eyebrows will be changed during ahegao.");
            CfgSetEyes = Config.Bind(s, "Set Eyes", true, "Whether eyes will be changed during ahegao.");
            CfgSetMouth = Config.Bind(s, "Set Mouth", true, "Whether the mouth will be changed during ahegao.");
            CfgSpeedToggle = Config.Bind(s, "Speed Toggle", true, "When enabled, ahegao only happens above 50% speed.");
            CfgMinSpeed = Config.Bind(s, "Minimum Speed", 66, new ConfigDescription("Minimum speed to toggle ahegao. Only checked if the \n speed toggle is enabled in the first place.", new AcceptableValueRange<int>(0, 100)));

            CfgAhegaoEyebrow = Config.Bind(s, "Ahegao Eyebrow ID", 2, "ID of the eyebrow expression to set during ahegao.");
            CfgAhegaoEyes = Config.Bind(s, "Ahegao Eye ID", 25, "ID of the eye expression to set during ahegao.");
            CfgAhegaoMouth = Config.Bind(s, "Ahegao Mouth ID", 24, "ID of the mouth expression to set during ahegao.");

            CfgRollEnabled = Config.Bind(s, "Eye Rolling", true, "When enabled, the eyes will roll back during the ahegao state.");
            CfgEyeY = Config.Bind(s, "Eye Roll Amount", 0.25f, new ConfigDescription("How much the eyes should roll.", new AcceptableValueRange<float>(0, 0.5f)));
            _newOffset = new Vector2(_originalOffset.x, _originalOffset.y - CfgEyeY.Value * 2f);
            CfgEyeY.SettingChanged += (sender, args) =>
            {
                _newOffset.y = _originalOffset.y - CfgEyeY.Value * 2f;
            };


            CfgSetTears = Config.Bind(s, "Set Tears", true, "Whether tears will be displayed during ahegao or not.");
            CfgSetBlush = Config.Bind(s, "Set Blush", true, "Whether the custom blush amount will be displayed during ahegao.");
            CfgAhegaoTears = Config.Bind<byte>(s, "Tears Level", 0, new ConfigDescription("The level of tears to display during ahegao.\n0 is none.", new AcceptableValueList<byte>(0, 1, 2, 3)));
            CfgAhegaoBlush = Config.Bind(s, "Blush Level", 0f, new ConfigDescription("The level of blush displayed during ahegao.\n0 for none.", new AcceptableValueRange<float>(0f, 1f)));

            AhegaoHotkey = Config.Bind("", "Reset Ahegao Hotkey", new KeyboardShortcut(KeyCode.O), "Resets the orgasm count to zero.");

            if (detectDark)
                CfgDarkEnabled = Config.Bind(s, "Darkness Toggle", true, "Whether or not ahegao can trigger in the Darkness mode.");

            Config.SettingChanged += (sender, args) => RefreshFace();
        }

        private void SceneLoaded(Scene s, LoadSceneMode lsm)
        {
            _hSceneProc = FindObjectOfType(_hSceneType);
            if (!_instance.enabled && _hSceneProc != null)
            {
                _hold = false;
                _fastEnough = false;
                _animName = "";
                _lstFemale = null;
                // Can't run coroutines on this instance because it's disabled
                ThreadingHelper.Instance.StartCoroutine(SceneLoadedAsync(_hSceneProc));
            }
        }

        private IEnumerator SceneLoadedAsync(Object proc)
        {
            var traverse = Traverse.Create(proc);

            while (_lstFemale == null || _lstFemale?.Count == 0)
            {
                _lstFemale = traverse.Field(nameof(HSceneProc.lstFemale)).GetValue<List<ChaControl>>();
                yield return null;
            }

            object handCtrlObj = null;
            while (handCtrlObj == null)
            {
                if (_isVr)
                    handCtrlObj = traverse.Field("vrHands").GetValue<object[]>().FirstOrDefault(x => x != null);
                else
                    handCtrlObj = traverse.Field(nameof(HSceneProc.hand)).GetValue<object>();

                yield return null;
            }

            var handCtrlType = Type.GetType(_isVr ? "VRHandCtrl, Assembly-CSharp" : "HandCtrl, Assembly-CSharp");
            var isKissMethod = AccessTools.Method(handCtrlType, nameof(HandCtrl.IsKissAction));
            _isKissActionDelegate = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), handCtrlObj, isKissMethod);
            _hflags = traverse.Field(nameof(HSceneProc.flags)).GetValue<HFlag>();

            ReloadConfig();
            ResetAhegao(); //Reset orgasm counter on H scene entry.                     
            if (_hi == null)
            {
                _hi = Harmony.CreateAndPatchAll(typeof(Hooks));
                if (_isVr)
                {
                    _hi.Patch(AccessTools.Method(_hSceneType, nameof(HSceneProc.EndProc)),
                        postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.EndProc)));
                }
            }

            enabled = true;
        }

        private static void RemovePatches()
        {
            if (_hi != null)
            {
                _hi.UnpatchAll(_hi.Id);
                _hi = null;
            }
        }

        private void ReloadConfig()
        {
            Config.Reload();
            RefreshFace();
        }


        private static void AddOrgasm()
        {
            if (!_isValidMode) return;
            if (!_instance.enabled) return;
            if (++_orgasmCount >= AhegaoCount.Value) RefreshFace();
        }

        private static void RefreshFace()
        {
            if (!_isValidMode) return;
            foreach (var female in _lstFemale)
            {
                if (female.eyesCtrl.FBSTarget.Any(x => x.GetSkinnedMeshRenderer() == null))
                    continue; //Apparently sharedMesh can nullref
                female.ChangeEyebrowPtn(0);
                female.ChangeEyesPtn(0);
                female.ChangeMouthPtn(0);
            }
        }

        private static void ResetAhegao()
        {
            _orgasmCount = 0;
            RefreshFace();
        }

        private void Update()
        {
            if (_lstFemale == null || _lstFemale.Count == 0) return;
            if (AhegaoHotkey.Value.IsDown()) ResetAhegao();
            //if (ReloadConfigHotkey.Value.IsDown()) ReloadConfig();

            foreach (var cc in _lstFemale)
            {
                if (cc == null) return;
                if (_hSceneProc == null) return;

                if (_isKissActionDelegate?.Target != null)
                {
                    var ik = _isKissActionDelegate.Invoke();
                    var isKissJustChanged = !_isKiss && ik;
                    _isKiss = ik;
                    if (isKissJustChanged) RefreshFace();
                }
                if (_isKiss) return;

                //Force refresh of face when adjusting speed.
                var fe = _hflags.speed >
                         CfgMinSpeed.Value *
                         0.03f; //3f is the max speed, but cfgMinSpeed displays between 1 and 100 to the user for ease of use
                if (fe != _fastEnough)
                {
                    _fastEnough = fe;
                    RefreshFace();
                }

                //Allow face state to remain when transitioning to orgasm animation.
                var aci = cc.animBody.GetCurrentAnimatorClipInfo(0);
                if (aci.Length == 0) return;
                var an = aci[0].clip.name;
                if (_animName != an)
                {
                    _hold = false;
                    _animName = an;
                    var mode = _hflags.mode.ToString();
                    var isAibu = mode == nameof(HFlag.EMode.aibu);  // caress mode
                    _isDarkness = mode == nameof(HFlag.EMode.sonyu3PMMF);
                    _isValidMode = isAibu || _isDarkness && CfgDarkEnabled.Value ||
                                   mode == nameof(HFlag.EMode.sonyu) || mode == nameof(HFlag.EMode.sonyu3P);
                    var hl = an.Contains("Loop1") || an.Contains("Loop2") || an.Contains("OLoop") ||
                             an.Contains("IN_Start") || an.Contains("IN_Loop") || an.Contains("_IN_A");
                    if (hl)
                    {
                        _hold = true;
                    }
                    else if (!isAibu)
                    {
                        var rf = an.Contains("Idle") || an.Contains("OUT_A") || an.Contains("OUT_Start") || an.Contains("OUT_Loop");
                        // || an.Contains("_IN_A") || an.Contains("OUT_Start") || an.Contains("OUT_Loop") || an.Contains("OUT_A");
                        if (rf)
                        {
                            _hflags.speed = 0f;
                            RefreshFace();
                        }
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (_lstFemale == null || _lstFemale.Count == 0) return;
            foreach (var cc in _lstFemale)
            {
                if (cc == null) return;
                if (_hSceneProc == null) return;
                Roll(cc, ShouldProc() && !ShouldNotProc(cc) && CfgRollEnabled.Value);
            }
        }

        private static bool ShouldNotProc(ChaControl female)
        {
            return !_isValidMode || !_instance.enabled || female == null || _orgasmCount < AhegaoCount.Value || _isKiss;
        }

        private static bool ShouldProc()
        {
            return !(CfgSpeedToggle.Value && !_fastEnough || !_hold);
        }

        private static void Roll(ChaControl target, bool roll)
        {
            if (roll)
            {
                foreach (var elmc in target.eyeLookMatCtrl)
                {
                    foreach (var tS in elmc.texStates)
                    {
                        var textureOffset = elmc._renderer.material.GetTextureOffset(tS.texID);
                        textureOffset.y -= CfgEyeY.Value;
                        elmc._renderer.material.SetTextureOffset(tS.texID, textureOffset);
                    }
                }

                // Unlike the above, this offset doesn't reset itself each frame, so we have to make sure it doesn't
                // just shoot off into space. The multiplication by 2 in the newOffset creation is because they seem to be on different scales, and
                // 2 is my best approximation                
                target.rendEye[0].material.SetTextureOffset(ChaShader._expression, _newOffset);
                target.rendEye[1].material.SetTextureOffset(ChaShader._expression, _newOffset);
            }
            else
            {
                // I'm unsure if this part is actually required (I don't think the offset matters if the texture doesn't show
                // after all), but it's probably better to be safe since as I noted before, this offset doesn't get reset autoatically
                target.rendEye[0].material.SetTextureOffset(ChaShader._expression, _originalOffset);
                target.rendEye[1].material.SetTextureOffset(ChaShader._expression, _originalOffset);
            }
        }

        private static class Hooks
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddAibuOrg))] //Foreplay
            public static void AddAibuOrg()
            {
                AddOrgasm();
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuOrg))] //Vaginal
            public static void AddSonyuOrg()
            {
                AddOrgasm();
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuAnalOrg))] //Anal
            public static void AddSonyuAnalOrg()
            {
                AddOrgasm();
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeEyebrowPtn))]
            public static void ChangeEyebrowPtn(ChaControl __instance, ref int ptn)
            {
                if (CfgSetEyeBrows.Value) ChangePtn(ref __instance, ref ptn, CfgAhegaoEyebrow.Value);
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeEyesPtn))]
            public static void ChangeEyesPtn(ChaControl __instance, ref int ptn)
            {
                if (CfgSetEyes.Value) ChangePtn(ref __instance, ref ptn, CfgAhegaoEyes.Value);
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeMouthPtn))]
            public static void ChangeMouthPtn(ChaControl __instance, ref int ptn)
            {
                if (CfgSetMouth.Value) ChangePtn(ref __instance, ref ptn, CfgAhegaoMouth.Value);
            }

            private static void ChangePtn(ref ChaControl __instance, ref int ptn, int newPtn)
            {
                //if we're not in a H-scene or are kissing, do not change the pattern
                if (!_isValidMode) return;
                if (_isKiss) return;
                if (!_instance.enabled || !_lstFemale.Contains(__instance) || _orgasmCount < AhegaoCount.Value) return;
                if (CfgSpeedToggle.Value && !_fastEnough || !_hold)
                {
                    //don't set the face to 0 during darkness since that isn't the default
                    if (!_isDarkness) ptn = 0;
                }
                else
                {
                    ptn = newPtn;
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(HSceneProc), nameof(HSceneProc.EndProc))]
            public static void EndProc()
            {
                if (_instance.enabled)
                {
                    _instance.enabled = false;
                    RemovePatches();
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(FaceListCtrl), nameof(FaceListCtrl.SetFace))]
            public static void SetFace(ChaControl _chara, bool __result)
            {
                if (!__result) return;
                if (ShouldNotProc(_chara)) return;
                if (!ShouldProc()) return;
                if (CfgSetTears.Value) _chara.tearsLv = CfgAhegaoTears.Value;
                if (CfgSetBlush.Value) _chara.ChangeHohoAkaRate(CfgAhegaoBlush.Value);
            }
        }
    }
}