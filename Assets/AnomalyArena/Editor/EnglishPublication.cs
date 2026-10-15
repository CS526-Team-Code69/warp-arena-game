using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace AnomalyArena.EditorTools
{
    public static class EnglishPublication
    {
        [Serializable] sealed class Entries { public Entry[] entries; }
        [Serializable] sealed class Entry { public string zh, en; }

        public static void Apply()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-translationMap");
            if(index<0 || index+1>=args.Length)throw new ArgumentException("A translation map is required.");
            var map=JsonUtility.FromJson<Entries>(File.ReadAllText(args[index+1])).entries.ToDictionary(e=>e.zh,e=>e.en);
            const string root="Assets/AnomalyArena/Localization/";
            var english=AssetDatabase.LoadAssetAtPath<StringTable>(root+"Strings/ArenaUI_en.asset");
            var retained=AssetDatabase.LoadAssetAtPath<StringTable>(root+"Strings/ArenaUI_zh-Hans.asset");
            foreach(var entry in english.Values)retained.AddEntry(entry.KeyId,entry.Value);
            EditorUtility.SetDirty(retained);
            var locale=AssetDatabase.LoadAssetAtPath<Locale>(root+"Locales/en.asset");
            AssetDatabase.SaveAssets();
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=locale;
            const string scenePath="Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
            var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
            int changed=0;
            foreach(var go in scene.GetRootGameObjects())
            {
                foreach(var component in go.GetComponentsInChildren<Component>(true))
                {
                    if(!component)continue;
                    if(component is SceneLocalePreset preset)preset.StartingLocale=locale;
                    if(component is ArenaLocaleButton selector)selector.gameObject.SetActive(false);
                    if(component is UnityEngine.Localization.Components.LocalizeStringEvent localized)localized.StringReference.LocaleOverride=locale;
                    if(component is UnityEngine.UI.Text label && HasCjk(label.text))
                    {
                        if(!map.TryGetValue(label.text,out var translated))throw new InvalidOperationException("Untranslated label: "+label.name);
                        label.text=translated;changed++;
                    }
                    var serialized=new SerializedObject(component);
                    var property=serialized.GetIterator();
                    bool dirty=false;
                    while(property.Next(true))
                    {
                        if(property.propertyType!=SerializedPropertyType.String || !HasCjk(property.stringValue))continue;
                        if(!map.TryGetValue(property.stringValue,out var value))throw new InvalidOperationException("Untranslated scene property: "+component.name+" / "+property.propertyPath);
                        property.stringValue=value;dirty=true;changed++;
                    }
                    if(dirty)serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(component);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            var hud=UnityEngine.Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
            if(hud && hud.artLabel)hud.artLabel.text="ART: WHITEBOX";
            int labels=0;
            foreach(var label in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>())
            {
                if(label.gameObject.scene!=scene)continue;
                labels++;
                if(HasCjk(label.text))
                {
                    if(!map.TryGetValue(label.text,out var value))throw new InvalidOperationException("Untranslated saved label: "+label.name);
                    label.text=value;EditorUtility.SetDirty(label);changed++;
                }
            }
            if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("English scene save failed.");
            if(!EditorSceneManager.SaveScene(scene,"Assets/AnomalyArena/Scenes/ArenaV2.unity",true))throw new InvalidOperationException("Authoring scene save failed.");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};
            PlayerSettings.productName="WARP ARENA";
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/english-publication.txt",$"Saved English scenes and string tables. Roots: {scene.rootCount}, labels: {labels}, replaced strings: {changed}. Compilation failed: {EditorUtility.scriptCompilationFailed}.");
            Debug.Log("English publication setup complete.");
        }

        static bool HasCjk(string value)=>value!=null && value.Any(c=>c>=0x3400 && c<=0x9fff);
    }
}
