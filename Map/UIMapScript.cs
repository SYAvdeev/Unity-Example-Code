using AiSystem;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIMapScript : MonoBehaviour
{
    static UIMapScript instance;
    public static UIMapScript GetInstance()
    {
        if (!instance)
        {
            instance = FindObjectOfType<UIMapScript>();
        }
        return instance;
    }

    public Vector2 MapZeroAnchor;

    const float ratio = .747605f;
    [HideInInspector] public float koeff = 4150f / 750f;

    [SerializeField] GameObject DrawnMap;
    RectTransform DrawnMapRect;

    
    [SerializeField] Sprite PlayerMark;
    
    [SerializeField] Sprite TeleporterMark;

    [Header("Fort Marks")]
    [SerializeField] Sprite CatFortMark;
    [SerializeField] Sprite DogFortMark;
    [SerializeField] Sprite RatFortMark;
    [SerializeField] Sprite ZombieFortMark;
    [SerializeField] Sprite NobodysFortMark;

    [Header("Army Marks")]
    [SerializeField] Sprite CatArmyMark;
    [SerializeField] Sprite DogArmyMark;
    [SerializeField] Sprite RatArmyMark;
    [SerializeField] Sprite ZombieArmyMark;

    UIMapMarkedObject MarkedPlayer;
    List<UIMapFortMark> MarkedForts = new List<UIMapFortMark>();
    List<UIMapTeleporterMark> TeleporterMarks = new List<UIMapTeleporterMark>();
    List<UIMapMarkedObject> TrackableObjects = new List<UIMapMarkedObject>();

    [Header("Fog Radius")]
    [SerializeField] float R1 = .03f;
    [SerializeField] float R2 = .04f;
    [SerializeField] Image FogImage;
    [SerializeField] Material FogOfWarMaterial;
    [SerializeField] Material FogToScreenMaterial;


    RenderTexture FogRenderTex1;
    RenderTexture FogRenderTex2;
    float curTime = 0f; 
    float updateInterval = 0.5f;


    public bool DebugTeleport = false;

    // Use this for initialization
    void Awake ()
    {
        GameObject player = LevelData.GetInstance().GetPlayerInstance();
        List<CreatureSpawn> creatureSpawns = CreatureSpawn._allBattleSpawnPoints;

        MarkedPlayer = CreateMark(player, PlayerMark, "PlayerMark");
        
        TrackableObjects.Add(MarkedPlayer);

        foreach (CreatureSpawn spawn in creatureSpawns)
        {
            UIMapFortMark mark = CreateFortMark(spawn.gameObject, NobodysFortMark, CatArmyMark, spawn.name + " Mark", spawn);
            MarkedForts.Add(mark);
        }

        foreach(Teleporter tel in Teleporter.AllGameTeleporters)
        {
            tel.DiscoveredEvent.AddListener(() => SetTeleportMarkActive(tel));
            UIMapTeleporterMark mark = CreateTeleporterMark(tel.gameObject, TeleporterMark, "Teleporter", tel);
            TeleporterMarks.Add(mark);
        }

        DrawnMapRect = DrawnMap.GetComponent<RectTransform>();
        koeff = ratio * DrawnMapRect.rect.width / 750f;

        FogRenderTex1 = new RenderTexture((int)DrawnMapRect.rect.width, (int)DrawnMapRect.rect.height, 0);
        FogRenderTex2 = new RenderTexture((int)DrawnMapRect.rect.width, (int)DrawnMapRect.rect.height, 0);

        Graphics.Blit(FogImage.sprite.texture, FogRenderTex1);


        FogOfWarMaterial.SetFloat("_R1", R1);
        FogOfWarMaterial.SetFloat("_R2", R2);
    }

    private void Start()
    {
        SetTeleportsPressable(false);
    }

    // Update is called once per frame
    void Update ()
    {
        if (curTime > updateInterval)
        {
            foreach (var obj in TrackableObjects)
                UpdateMarkedPos(obj);

            //foreach (var obj in MarkedForts)
                //UpdateTrackablePos(obj);

            UpdateFortImages();

            UpdateArmiesMarks();

            UpdateFogOfWar();

            curTime = 0f;
        }
        else
        {
            curTime += Time.deltaTime;
        }
    }    

    Vector3 ToMapCoords(Vector3 mapCoords)
    {
        return new Vector3(koeff * mapCoords.x, koeff * mapCoords.z);
    }

    void UpdateArmiesMarks()
    {
        foreach (var fort in MarkedForts)
        {
            Color color = fort.Army.MarkImage.color;

            if (fort.Fort.GetFighters() > 0)
            {
                AI_Pawn pawn = fort.Fort.FortGuardians[0];

                if (pawn != null)
                {
                    if (pawn.AiPlans == AI_Pawn.EAiPlans.CapturePoint)
                    {
                        if (pawn.flockLeader != null && fort.Army.SceneObject != pawn.flockLeader.gameObject)
                        {
                            fort.Army.SceneObject = pawn.flockLeader.gameObject;
                            
                            fort.Army.MarkImage.color = new Color(color.r, color.g, color.b, 1f);
                            continue;
                        }
                    }
                }
            }
            
            fort.Army.MarkImage.color = new Color(color.r, color.g, color.b, 0f);
        }
    }

    void UpdateFortImages()
    {
        foreach (var fort in MarkedForts)
        {
            switch(fort.Fort.creaturePointType)
            {
                case Pawn.ECreatureType.Cat:
                    fort.MarkImage.sprite = CatFortMark;
                    fort.Army.MarkImage.sprite = CatArmyMark;
                    break;
                case Pawn.ECreatureType.Dog:
                    fort.MarkImage.sprite = DogFortMark;
                    fort.Army.MarkImage.sprite = DogArmyMark;
                    break;
                case Pawn.ECreatureType.Rat:
                    fort.MarkImage.sprite = RatFortMark;
                    fort.Army.MarkImage.sprite = RatArmyMark;
                    break;
                case Pawn.ECreatureType.Zombie:
                    fort.MarkImage.sprite = ZombieFortMark;
                    fort.Army.MarkImage.sprite = ZombieArmyMark;
                    break;
                default:
                    fort.MarkImage.sprite = NobodysFortMark;
                    break;
            }
        }
    }

    GameObject CreateMarkBase(Sprite markImage, string name)
    {
        GameObject markGameObj = new GameObject(name);

        markGameObj.transform.SetParent(DrawnMap.transform, false);

        Image image = markGameObj.AddComponent<Image>();
        image.sprite = markImage;
        image.SetNativeSize();
        
        (markGameObj.transform as RectTransform).anchorMin = MapZeroAnchor;
        (markGameObj.transform as RectTransform).anchorMax = MapZeroAnchor;

        return markGameObj;
    }

    UIMapMarkedObject CreateMark(GameObject sceneObject, Sprite markImage, string name)
    {
        GameObject markGameObj = CreateMarkBase(markImage, name);

        UIMapMarkedObject mark = markGameObj.AddComponent<UIMapMarkedObject>();
        mark.SceneObject = sceneObject;
        mark.MarkImage = markGameObj.GetComponent<Image>();

        UpdateMarkedPos(mark);

        return mark;
    }

    UIMapTeleporterMark CreateTeleporterMark(GameObject sceneObject, Sprite markImage, string name, Teleporter teleporter)
    {
        GameObject markGameObj = CreateMarkBase(markImage, name);

        UIMapTeleporterMark teleport = markGameObj.AddComponent<UIMapTeleporterMark>();
        teleport.SceneObject = sceneObject;
        teleport.MarkImage = markGameObj.GetComponent<Image>();
        teleport.Teleporter = teleporter;

        Button button = markGameObj.AddComponent<Button>();
        button.onClick.AddListener(teleport.Teleport);

        UpdateMarkedPos(teleport);

        markGameObj.SetActive(false);

        return teleport;
    }

    UIMapFortMark CreateFortMark(GameObject sceneObject, Sprite markImage, Sprite armyMarkImage, string name, CreatureSpawn creatureSpawn)
    {
        GameObject markGameObj = CreateMarkBase(markImage, name);

        UIMapFortMark fort = markGameObj.AddComponent<UIMapFortMark>();
        fort.SceneObject = sceneObject;
        fort.MarkImage = markGameObj.GetComponent<Image>();
        fort.Fort = creatureSpawn;
        fort.Army = CreateMark(sceneObject, armyMarkImage, "Army of " + name);

        UpdateMarkedPos(fort);

        return fort;
    }

    void UpdateMarkedPos(UIMapMarkedObject obj)
    {
        (obj.transform as RectTransform).anchoredPosition = ToMapCoords(obj.ObjectCoords.position);
    }


    void UpdateFogOfWar()
    {
        Vector2 catPos = (MarkedPlayer.transform as RectTransform).anchoredPosition;
        Vector2 catPosPiv = (MarkedPlayer.transform as RectTransform).localPosition;
        Vector2 catPosUV = new Vector2((catPos.x + DrawnMapRect.rect.width * MapZeroAnchor.x) / DrawnMapRect.rect.width, (catPos.y + DrawnMapRect.rect.height * MapZeroAnchor.y) / DrawnMapRect.rect.height);
        

        FogOfWarMaterial.SetTexture("_FogTex", FogRenderTex1);
        FogOfWarMaterial.SetFloat("_CatCoordsX", catPosUV.x);
        FogOfWarMaterial.SetFloat("_CatCoordsY", catPosUV.y);
        

        Graphics.Blit(FogRenderTex1, FogRenderTex2, FogOfWarMaterial);
        Graphics.Blit(FogRenderTex2, FogRenderTex1);

        FogToScreenMaterial.SetTexture("_FogTex", FogRenderTex1);
        
    }

    public void SetTeleportsPressable(bool pressable)
    {
        foreach(UIMapTeleporterMark teleport in TeleporterMarks)
            teleport.MakePressable(pressable);
    }

    public void SetTeleportMarkActive(Teleporter teleporter)
    {
        UIMapTeleporterMark mark = TeleporterMarks.Find(t => t.Teleporter == teleporter);

        mark.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        SetTeleportsPressable(false);
    }
}