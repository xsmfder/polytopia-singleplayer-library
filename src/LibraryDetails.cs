using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
namespace SinglePlayerLibrary
{
    internal sealed partial class LibraryRuntime
    {
        GameObject details,detailNotice;
        RectTransform detailsPanel,detailNoticePanel;
        string detailPath,detailHash,detailTitle,detailDescription;
        bool detailReplay,deleteConfirmation;
        EntryInfo detailInfo;
        string[] detailValues;
        internal bool DetailsOpen=>details!=null;
        internal void ShowItemDetails(string path,bool replay)
        {
            try
            {
                byte[] file=LibraryStore.ReadBounded(path);var data=LibraryStore.Unpack(file);
                var client=NativeSession.Decode(data.Payload,data.Info.NativeEncoding=="wrapped-client");
                var state=client.GameState;var settings=state.Settings;
                detailPath=path;detailHash=LibraryStore.Hash(file);detailReplay=replay;detailInfo=data.Info;
                string mode=NativeSession.ModeName(settings);
                detailValues=new[]{((long)state.Map.Width*state.Map.Height).ToString(),mode,settings.mapPreset.ToString(),DetailTranslation(GameModeUtils.GetDifficultyName(settings.Difficulty),settings.Difficulty.ToString()),settings.OpponentCount.ToString(),state.Map.Width+" × "+state.Map.Height};
                detailTitle=String.IsNullOrWhiteSpace(settings.GameName)?data.Info.Tribe+" · "+data.Info.Mode:settings.GameName;
                detailDescription=DetailTranslation(GameModeUtils.GetDescription(settings.RulesGameMode),mode=="Infinity"?"Play without a victory condition.":mode=="Perfection"?"Get the highest score in 30 turns":"Destroy all opponents to win");
                BuildDetails(false);
            }
            catch(Exception e){Error(e);RefreshRows();}
        }
        internal static string DetailModeIcon(string mode)
        {
            switch(mode)
            {
                case "Perfection":return "UI_perfection";
                case "Domination":return "UI_domination";
                case "Glory":return "UI_glory";
                case "Might":return "UI_might";
                case "Infinity":case "Custom":case "Sandbox":return "UI_sandbox";
                default:return null;
            }
        }
        static string DetailTranslation(string key,string fallback)
        {
            if(String.IsNullOrWhiteSpace(key))return fallback;
            string text=I2.Loc.LocalizationManager.GetTranslation(key,true,0,true,false,null,null,true);
            return String.IsNullOrWhiteSpace(text) || text==key?fallback:text;
        }
        void BuildDetails(bool confirm)
        {
            CloseDetails();deleteConfirmation=confirm;
            var bg=page.transform.GetChild(0).gameObject;var group=bg.GetComponent<CanvasGroup>() ?? bg.AddComponent<CanvasGroup>();group.interactable=false;
            var cover=Panel(page.transform,"SinglePlayerLibrary.Details",new Color(0,0,0,.55f));details=cover.gameObject;Stretch(cover,0,0,0,0);
            detailsPanel=Panel(cover,"Details panel",Color.black);detailsPanel.GetComponent<Image>().sprite=roundedSprite;detailsPanel.GetComponent<Image>().type=Image.Type.Sliced;
            detailsPanel.anchorMin=detailsPanel.anchorMax=new Vector2(.5f,.5f);detailsPanel.sizeDelta=new Vector2(450,confirm?240:detailReplay?370:490);
            if(confirm)
            {
                var header=Label(detailsPanel,"Delete this "+(detailReplay?"replay":"saved game")+"?",28,rowFont);Layout(header.rectTransform,16,22,418,40);
                var body=Label(detailsPanel,"This removes the selected entry.\nOther saved games and replays are kept.",19,bodyFont);Layout(body.rectTransform,16,77,418,72);body.enableWordWrapping=true;
                var cancel=ActionButton(detailsPanel,"CANCEL",()=>BuildDetails(false),Color.white,new Color(.3f,.3f,.3f));Layout(cancel.GetComponent<RectTransform>(),16,172,190,44);
                var remove=ActionButton(detailsPanel,"DELETE",DeleteDetail,new Color(.85f,.12f,.09f),Color.white);Layout(remove.GetComponent<RectTransform>(),244,172,190,44);
                Select(cancel);UpdateDetailsLayout();return;
            }
            var title=Label(detailsPanel,detailTitle,32,rowFont);Layout(title.rectTransform,16,12,304,44);title.enableAutoSizing=true;title.fontSizeMin=20;title.fontSizeMax=32;title.overflowMode=TextOverflowModes.Ellipsis;
            var delete=ActionButton(detailsPanel,"DELETE",()=>BuildDetails(true),new Color(.85f,.12f,.09f),Color.white);Layout(delete.GetComponent<RectTransform>(),334,16,100,34);
            Sprite FindSprite(string name)=>Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sp=>sp.name==name);
            DetailCircle(0,"Map Type",detailValues[2],null,detailValues[0],()=>ShowDetailNotice("Map Type",detailValues[2]+"\n"+detailValues[0]+" tiles map"));
            DetailCircle(1,"Game Mode",detailValues[1],FindSprite(DetailModeIcon(detailValues[1])),null,()=>ShowDetailNotice(detailValues[1],"Game mode: "+detailValues[1]+"\n"+detailDescription));
            DetailCircle(2,"Opponents",detailValues[3],null,detailValues[4],()=>ShowDetailNotice("Opponents",detailValues[4]+" opponents\nDifficulty: "+detailValues[3]));
            DetailCircle(3,"More info","More info",FindSprite("UI_literacy"),null,()=>ShowDetailNotice("Game Info","ID: "+detailInfo.Id+"\nGame version: "+detailInfo.NativeVersion+"\nPolytopia: "+detailInfo.GameVersion+"\nUpdated: "+detailInfo.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")+"\nTurn: "+detailInfo.Turn));
            var description=Label(detailsPanel,detailDescription,24,bodyFont);Layout(description.rectTransform,24,193,402,66);description.alignment=TextAlignmentOptions.Center;description.enableWordWrapping=true;description.enableAutoSizing=true;description.fontSizeMin=18;description.fontSizeMax=24;
            if(detailReplay)
            {
                var outcome=Label(detailsPanel,detailInfo.Outcome=="Won"?"Victory":detailInfo.Outcome=="Lost"?"Defeat":"Completed replay",20,rowFont);Layout(outcome.rectTransform,24,265,402,28);outcome.alignment=TextAlignmentOptions.Center;
            }
            else
            {
                var copy=ActionButton(detailsPanel,"COPY",CopyDetail,new Color(0,.62f,.93f),Color.white);Layout(copy.GetComponent<RectTransform>(),115,279,220,44);
                var folder=ActionButton(detailsPanel,"OPEN FOLDER",()=>OpenLibraryFolder(false),new Color(0,.62f,.93f),Color.white);Layout(folder.GetComponent<RectTransform>(),115,339,220,44);
            }
            float footer=detailsPanel.sizeDelta.y-64;
            var back=ActionButton(detailsPanel,"BACK",CloseDetails,Color.white,new Color(.3f,.3f,.3f));Layout(back.GetComponent<RectTransform>(),16,footer,110,48);
            var open=ActionButton(detailsPanel,"OPEN",()=>OpenItem(detailPath,detailReplay),new Color(0,.62f,.93f),Color.white);Layout(open.GetComponent<RectTransform>(),324,footer,110,48);
            Select(open);UpdateDetailsLayout();
        }
        void DetailCircle(int index,string name,string caption,Sprite sprite,string number,Action action)
        {
            float x=39+index*100;
            var circle=CircleButton(detailsPanel,name,sprite,action,true);Layout(circle,x,72,72,72);
            if(number!=null)
            {
                var digits=Label(circle,number,30,rowFont);Stretch(digits.rectTransform,4,4,0,0);digits.alignment=TextAlignmentOptions.Center;digits.enableAutoSizing=true;digits.fontSizeMin=19;digits.fontSizeMax=30;
            }
            var label=Label(detailsPanel,caption,17,rowFont);Layout(label.rectTransform,x-15,150,102,28);label.alignment=TextAlignmentOptions.Center;label.enableAutoSizing=true;label.fontSizeMin=12;label.fontSizeMax=17;
        }
        void ShowDetailNotice(string title,string content)
        {
            CloseDetailNotice();var group=detailsPanel.GetComponent<CanvasGroup>() ?? detailsPanel.gameObject.AddComponent<CanvasGroup>();group.interactable=false;
            var cover=Panel(details.transform,"Detail information overlay",new Color(0,0,0,.25f));detailNotice=cover.gameObject;Stretch(cover,0,0,0,0);
            detailNoticePanel=Panel(cover,"Information panel",Color.black);detailNoticePanel.GetComponent<Image>().sprite=roundedSprite;detailNoticePanel.GetComponent<Image>().type=Image.Type.Sliced;
            detailNoticePanel.anchorMin=detailNoticePanel.anchorMax=new Vector2(.5f,.5f);detailNoticePanel.sizeDelta=new Vector2(462,240);
            var header=Label(detailNoticePanel,title,32,rowFont);Layout(header.rectTransform,16,16,430,44);
            var body=Label(detailNoticePanel,content,23,bodyFont);Layout(body.rectTransform,16,74,430,180);body.enableWordWrapping=true;
            float height=Math.Max(60,body.preferredHeight);Layout(body.rectTransform,16,74,430,height);detailNoticePanel.sizeDelta=new Vector2(462,height+150);
            var back=ActionButton(detailNoticePanel,"BACK",CloseDetailNotice,new Color(0,.62f,.93f),Color.white);Layout(back.GetComponent<RectTransform>(),176,height+86,110,48);
            Select(back);UpdateDetailsLayout();
        }
        void CloseDetailNotice()
        {
            if(detailNotice==null)return;
            detailNotice.SetActive(false);UnityEngine.Object.Destroy(detailNotice);detailNotice=null;detailNoticePanel=null;
            if(detailsPanel!=null){var group=detailsPanel.GetComponent<CanvasGroup>();if(group!=null)group.interactable=true;}
        }
        Button ActionButton(Transform parent,string name,Action action,Color background,Color text)
        {
            var button=TextButton(parent,name,action,true,25);var colors=button.colors;colors.normalColor=background;colors.highlightedColor=Color.Lerp(background,Color.white,.14f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=Color.Lerp(background,Color.black,.15f);button.colors=colors;
            var label=button.GetComponentInChildren<TextMeshProUGUI>();label.color=text;label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=25;return button;
        }
        static void Select(Button button){if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(button.gameObject);}
        internal void UpdateDetailsLayout()
        {
            if(detailsPanel==null || page==null)return;
            var area=page.GetComponent<RectTransform>().rect;
            void Fit(RectTransform panel){if(panel==null)return;float scale=Math.Min(1,Math.Min((area.width-24)/panel.sizeDelta.x,(area.height-24)/panel.sizeDelta.y));panel.localScale=new Vector3(scale,scale,1);}
            Fit(detailsPanel);Fit(detailNoticePanel);
        }
        internal void BackFromDetails(){if(detailNotice!=null)CloseDetailNotice();else if(deleteConfirmation)BuildDetails(false);else CloseDetails();}
        internal void CloseDetails()
        {
            CloseDetailNotice();
            if(details==null)return;
            details.SetActive(false);UnityEngine.Object.Destroy(details);details=null;detailsPanel=null;
            if(page!=null){var group=page.transform.GetChild(0).GetComponent<CanvasGroup>();if(group!=null)group.interactable=true;}
        }
        void CopyDetail()
        {
            if(detailReplay)return;
            try{Store.CopySave(detailPath,detailHash);CloseDetails();LastError=null;RefreshRows();}
            catch(Exception e){Error(e);CloseDetails();RefreshRows();}
        }
        void DeleteDetail()
        {
            if(!deleteConfirmation)return;
            try
            {
                Store.DeleteItem(detailPath,detailReplay,detailHash);
                // The native slot is only a cache of the selected ordinary game.
                // Remove its matching ID too, so vanilla Resume cannot resurrect it.
                if(!detailReplay)
                {
                    string native=Paths.GetSingleplayerFilePath(detailInfo.Id);
                    if(File.Exists(native))File.Delete(native);
                }
                CloseDetails();LastError=null;RefreshRows();
            }
            catch(Exception e){Error(e);CloseDetails();RefreshRows();}
        }
        void OpenLibraryFolder(bool replay)
        {
            try
            {
                string folder=replay?Store.Replays:Store.Saves;Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{FileName=Path.GetFullPath(folder),UseShellExecute=true,Verb="open"});LastError=null;
            }
            catch(Exception e){Error(e);}
        }
    }
}
