using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using MonoMod.Utils;
using UnityEngine;
using UnityEngine.UI;

public class MyScript : MonoBehaviour
{
    public Text TextField;
    
    private void Start()
    {
        
        Harmony.CreateAndPatchAll(typeof(MyScript));
        // var h = new HarmonyLib.Harmony("test");
        // PatchManager.ResolvePatcher += 
        // h.PatchAll();
        // il2cppchain
    }

    // Update is called once per frame
    void Update()
    {
        TextField.text = GetText();
    }

    public string GetText()
    {
        return "didnt work";
    }

    
    [HarmonyPostfix]
    [HarmonyPatch( typeof(MyScript), "GetText")]
    private static void Postfix(ref string __result)
    {
        __result = "patch worked";
    }

    // private class Patcher : MethodPatcher
    // {
    //     public Patcher(MethodBase original) : base(original)
    //     {
    //         
    //     }
    //
    //     public override DynamicMethodDefinition PrepareOriginal()
    //     {
    //         
    //     }
    //
    //     public override MethodBase DetourTo(MethodBase replacement)
    //     {
    //     }
    //
    //     public override DynamicMethodDefinition CopyOriginal()
    //     {
    //     }
    // }
}
