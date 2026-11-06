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
            if(hud)FitEnglishText(hud);
            if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("English scene save failed.");
            if(!EditorSceneManager.SaveScene(scene,"Assets/AnomalyArena/Scenes/ArenaV2.unity",true))throw new InvalidOperationException("Authoring scene save failed.");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};
            PlayerSettings.productName="WARP ARENA";
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/english-publication.txt",$"Saved English scenes and string tables. Roots: {scene.rootCount}, labels: {labels}, replaced strings: {changed}. Compilation failed: {EditorUtility.scriptCompilationFailed}.");
            Debug.Log("English publication setup complete.");
        }

        /// <summary>The scene's layout is the Chinese edition's, whose slot boxes hold two or three characters. English names and details need
        /// more room, so here the slot titles and details take the slot's width on up to two lines and the icons make way; card texts get
        /// tighter lines, as the CJK font's line height is too tall for Latin text. Nothing else moves.</summary>
        static void FitEnglishText(V2Hud hud)
        {
            foreach(var slot in hud.handSlots)Slot(slot,106f,156f,new Vector2(132f,-38f),new Vector2(46f,24f),slot?slot.weaponIcon:null,slot?slot.contentIcon:null);
            foreach(var slot in hud.autoSlots)Slot(slot,96f,96f,Vector2.zero,Vector2.zero,slot?slot.weaponIcon:null,slot?slot.contentIcon:null);
            foreach(var slot in hud.skillSlots)Slot(slot,62f,96f,new Vector2(86f,-38f),new Vector2(24f,24f),slot?slot.weaponIcon:null,slot?slot.contentIcon:null);
            foreach(var screen in hud.screens)
            {
                if(!screen||screen.choices==null)continue;
                foreach(var choice in screen.choices)
                    if(choice&&choice.body){choice.body.lineSpacing=.9f;EditorUtility.SetDirty(choice.body);}
            }
        }

        static void Slot(V2UiSlot slot,float titleWidth,float detailWidth,Vector2 iconAt,Vector2 iconSize,params UnityEngine.UI.Image[] icons)
        {
            if(!slot)return;
            Box(slot.title,-33f,titleWidth,30f,10,15);
            Box(slot.detail,-62f,detailWidth,27f,9,13);
            foreach(var icon in icons)
            {
                if(!icon)continue;
                icon.rectTransform.anchoredPosition=iconAt;icon.rectTransform.sizeDelta=iconSize;
                EditorUtility.SetDirty(icon.rectTransform);
            }
        }

        static void Box(UnityEngine.UI.Text text,float top,float width,float height,int smallest,int largest)
        {
            if(!text)return;
            var rect=text.rectTransform;
            rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,top);rect.sizeDelta=new Vector2(width,height);
            text.resizeTextForBestFit=true;text.resizeTextMinSize=smallest;text.resizeTextMaxSize=largest;text.fontSize=largest;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.lineSpacing=.85f;
            EditorUtility.SetDirty(text);EditorUtility.SetDirty(rect);
        }

        static bool HasCjk(string value)=>value!=null && value.Any(c=>c>=0x3400 && c<=0x9fff);
    }
}
