using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PlayableGames;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Transform m_Player;
    [SerializeField] private List<Transform> m_Spawn_LOCs;
    [Serializable] public class HotelState
    {
        public int cash=25, rooms=1, level=1, guests=1, served, pending;
        public float[] stays=new float[4]; public long savedAt;
    }
    private HotelState state=new HotelState();
    private Text money, guidance, queue;
    private readonly Text[] roomLabels=new Text[4];
    private Button checkIn, collect, expand, upgrade;
    private float arrivals, refresh;
    public int Cash => state.cash;
    public int Rooms => state.rooms;
    public int Served => state.served;
    private int RoomCost => 60*state.rooms*state.rooms;
    private int UpgradeCost => 45*state.level;
    private int StayValue => 25+10*(state.level-1);
    private void Start()
    {
        string json=PlayerPrefs.GetString("grandstay.hotel.v1","");
        if(!string.IsNullOrEmpty(json))
        {
            try { var loaded=JsonUtility.FromJson<HotelState>(json); if(loaded!=null && loaded.rooms>=1 && loaded.rooms<=4 && loaded.stays!=null && loaded.stays.Length==4) state=loaded; } catch(Exception) { }
        }
        foreach(Canvas c in FindObjectsOfType<Canvas>()) c.gameObject.SetActive(false);
        var panel=NativeUi.Create("Grandstay hotel management");
        NativeUi.Label(panel,"GRANDSTAY",.05f,.86f,.9f,.07f,36);
        NativeUi.Label(panel,"A small hotel. A growing dream.",.05f,.81f,.9f,.045f,18);
        money=NativeUi.Label(panel,"",.04f,.735f,.92f,.065f,27);
        queue=NativeUi.Label(panel,"",.05f,.68f,.9f,.045f,20);
        for(int i=0;i<4;i++)
        {
            float x=i%2==0 ? .07f : .52f;float y=i<2 ? .515f : .355f;
            var card=NativeUi.Place(panel,"Room "+(i+1),x,y,.41f,.14f);
            var image=card.gameObject.AddComponent<Image>();image.color=new Color(.07f,.17f,.25f);image.raycastTarget=false;
            roomLabels[i]=NativeUi.Label(card,"",.05f,.08f,.9f,.84f,22);
        }
        guidance=NativeUi.Label(panel,"Check in a guest, collect their payment, then expand your hotel.",.07f,.285f,.86f,.06f,17);
        checkIn=NativeUi.Button(panel,"CHECK IN",.07f,.195f,.41f,.075f,CheckIn);
        collect=NativeUi.Button(panel,"COLLECT",.52f,.195f,.41f,.075f,Collect);
        expand=NativeUi.Button(panel,"",.07f,.09f,.41f,.08f,Expand);
        upgrade=NativeUi.Button(panel,"",.52f,.09f,.41f,.08f,Upgrade);
        arrivals=5;UpdateUi();
    }
    private void Update()
    {
        float dt=Mathf.Min(Time.deltaTime,.2f);bool changed=false;
        for(int i=0;i<state.rooms;i++) if(state.stays[i]>0)
        {
            state.stays[i]-=dt;
            if(state.stays[i]<=0) { state.stays[i]=0;state.pending+=StayValue;state.served++;changed=true; }
        }
        arrivals-=dt;
        if(arrivals<=0) { arrivals=6;state.guests=Mathf.Min(4,state.guests+1);changed=true; }
        if(changed) Save();
        refresh-=dt;if(refresh<=0) {refresh=.2f;UpdateUi();}
    }
    public void CheckIn()
    {
        if(state.guests<=0) return;
        for(int i=0;i<state.rooms;i++) if(state.stays[i]<=0)
        { state.stays[i]=6;state.guests--;Save();UpdateUi();return; }
    }
    public void Collect()
    {
        if(state.pending<=0) return;
        state.cash+=state.pending;state.pending=0;Save();UpdateUi();
    }
    public void Expand()
    {
        if(state.rooms>=4 || state.cash<RoomCost) return;
        state.cash-=RoomCost;state.rooms++;Save();UpdateUi();
    }
    public void Upgrade()
    {
        if(state.cash<UpgradeCost) return;
        state.cash-=UpgradeCost;state.level++;Save();UpdateUi();
    }
    private void UpdateUi()
    {
        if(money==null) return;
        money.text="$"+state.cash+"  |  "+state.rooms+" room"+(state.rooms==1?"":"s");
        queue.text=state.guests+" waiting  •  "+state.served+" guests served";
        bool vacant=false;
        for(int i=0;i<4;i++)
        {
            bool open=i<state.rooms;bool busy=open && state.stays[i]>0;
            roomLabels[i].text=!open ? "ROOM "+(i+1)+"\nNot built" : busy ? "ROOM "+(i+1)+"\nGuest staying · "+Mathf.CeilToInt(state.stays[i])+"s" : "ROOM "+(i+1)+"\nReady for a guest";
            if(open && !busy) vacant=true;
        }
        checkIn.interactable=state.guests>0 && vacant;
        collect.interactable=state.pending>0;collect.GetComponentInChildren<Text>().text="COLLECT $"+state.pending;
        expand.interactable=state.rooms<4 && state.cash>=RoomCost;
        expand.GetComponentInChildren<Text>().text=state.rooms>=4 ? "HOTEL COMPLETE" : "ADD ROOM\n$"+RoomCost;
        upgrade.interactable=state.cash>=UpgradeCost;upgrade.GetComponentInChildren<Text>().text="BETTER SERVICE\n$"+UpgradeCost;
        guidance.text=state.rooms==4 ? "Your hotel is thriving! Improve service and welcome more guests." : "Stay pays $"+StayValue+". Collect earnings to add rooms or improve service.";
    }
    private void Save() {state.savedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds();PlayerPrefs.SetString("grandstay.hotel.v1",JsonUtility.ToJson(state));PlayerPrefs.Save();}
    private void OnApplicationPause(bool paused) {if(paused) Save();}
    private void OnApplicationQuit() {Save();}
}
