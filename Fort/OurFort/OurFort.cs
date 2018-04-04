using System;
using System.Linq;
using System.Collections.Generic;
using AiSystem;
using UnityEngine;
using Fort;
using UnityEngine.Events;
using UnityEngine.AI;

public class OurFort : MonoBehaviour
{
    OurFort() { }
    static OurFort instance;
    public static OurFort GetInstance()
    {
        if (!instance)
        {
            instance = FindObjectOfType<OurFort>();
        }
        return instance;
    }

    public Transform CameraView;

    public List<NavMeshSurface> NavMeshes;

    public InventoryBag CatBag;
    public KittenTray KittenTray;

    public BuildingMono MainBuildingMono;

    float SpeedOfProduction = 0f;

    int NumberOfCats = 0;
    public int MaxNumberOfCats = 2;

    //int NumberOfWorkers = 0;
    //int MaxNumberOfWorkers = 0;

    [Range(0, 100)]
    int Happiness;
    public int GetHappiness
    {
        get
        {
            return Happiness;
        }
    }

    public GameObject CreaturePrefab;

    public List<Cat> Cats { get; private set; }
    public Cat[] Warriors { get; private set; }
    public const int MaxNumberOfWarriors = 6;

    public List<BattleSquad> Squads = new List<BattleSquad>();
    public BattleSquad GetSquad(string name)
    {
        return Squads.Find(s => s.Name == name);
    }

    public Transform WarriorsSpawnPoint;
    public float SpawnRange = 2f;

    public ResourceProduction FoodProduction = new ResourceProduction() { Name = "FoodProduction", ResourceSpeed = 1f };
    public ResourceProduction WaterProduction = new ResourceProduction() { Name = "WaterProduction", ResourceSpeed = 1f };
    public ResourceProduction KittenProduction = new ResourceProduction() { Name = "KittenProduction", ResourceSpeed = 0.5f };
    public ItemsProduction ItemsProduction = new ItemsProduction() { Name = "ItemsProduction", ResourceSpeed = 1f };

    List<ResourceProduction> Productions = new List<ResourceProduction>();
    public ResourceProduction GetProduction(string name)
    {
        return Productions.Find(p => p.Name == name);
    }

    public List<BuildingMono> BuildingMonos = new List<BuildingMono>();
    public Storage Storage = new Storage() { FoodValue = 500, WaterValue = 500, Products = new List<Product>() };
    public List<Armor> AllGameArmor;

    public UnityEvent FortUpdated;

    public static void SwapInList<T>(IList<T> list, int indexA, int indexB)
    {
        T tmp = list[indexA];
        list[indexA] = list[indexB];
        list[indexB] = tmp;
    }

    public static void SwapInList<T>(IList<T> list, T A, T B)
    {
        SwapInList(list, list.IndexOf(A), list.IndexOf(B));
    }

    public static void MoveInList<T>(IList<T> list, int indexFrom, int indexTo)
    {
        T tempA = list[indexFrom];
        list.RemoveAt(indexFrom);
        list.Insert(indexTo, tempA);        
    }

    public static void MoveInList<T>(IList<T> list, T from, T to)
    {
        int indexFrom = list.IndexOf(from);
        int indexTo = list.IndexOf(to);

        MoveInList(list, indexFrom, indexTo);
    }

    void Awake()
    {
        if (CatBag == null)
            CatBag = FindObjectOfType<InventoryBag>();

        if (WarriorsSpawnPoint == null)
            WarriorsSpawnPoint = transform;

        Productions.Add(FoodProduction);
        Productions.Add(WaterProduction);
        Productions.Add(ItemsProduction);
        Productions.Add(KittenProduction);

        Cats = new List<Cat>();
        Warriors = new Cat[6];

        CreatureSpawn atacPoint = CreatureSpawn.FindRandomClosestCreaturePoint(Pawn.ECreatureType.Cat, transform.position);
        Squads = new List<BattleSquad>() { new BattleSquad() { Name = "Alpha", FortPosition = WarriorsSpawnPoint.position, AttackPoint = atacPoint },
                                           new BattleSquad() { Name = "Delta", FortPosition = WarriorsSpawnPoint.position, AttackPoint = atacPoint },
                                           new BattleSquad() { Name = "Omega", FortPosition = WarriorsSpawnPoint.position, AttackPoint = atacPoint }};

        InitNavMeshes();
    }
    

    void InitNavMeshes()
    {
        for (int i = 0; i < BuildingMonos.Count; i++)
        {
            BuildingMonos[i].Building.OnUpdated.AddListener(() => { NavMeshes.ForEach(n => n.BuildNavMesh()); });
        }

        NavMeshes.ForEach(n => n.BuildNavMesh());
    }

    public void AddToAvailableProducts(Product product)
    {
        if (!ItemsProduction.AvailableProducts.Contains(product))
        {
            ItemsProduction.AvailableProducts.Add(product);
        }
    }

    public void RemoveFromAvailableProducts(Product product)
    {
        if(ItemsProduction.AvailableProducts.Contains(product))
        {
            ItemsProduction.AvailableProducts.Remove(product);
        }
    }

    public void SetBuildingCollidersActive(bool active)
    {
        foreach(BuildingMono mono in BuildingMonos)
        {
            mono.SetCollidersActive(active);
        }
    }

    private void Start()
    {
        FortUpdated.Invoke();
        NavMeshes.ForEach(n => n.BuildNavMesh());
    }

    // Update is called once per frame
    void Update()
    {        
        CheckSpawn();
        Production();
        CalculateHappiness();
        Consumption();
        CheckWarriorsAlive();

        if (Input.GetKeyDown(KeyCode.B))
        {
            MainBuildingMono.Building.Upgrade();
            FortUpdated.Invoke();
        }
    }

    void CalculateHappiness()
    {
        if (Cats.Count > 0)
            Happiness = (int)(50 * Storage.FoodValue * Storage.WaterValue / 2f / Cats.Count);
        else
            Happiness = 100;
    }

    [Range(0, 300)]
    public float ProductionInterval = 60f;
    public float TimeTillProduction { get; private set; }
    void Production()
    {
        if (TimeTillProduction > ProductionInterval)
        {
            TimeTillProduction = 0f;

            Storage.FoodValue += FoodProduction.GetProducedResource();
            Storage.WaterValue += WaterProduction.GetProducedResource();

            if (ItemsProduction.ProductionQueue.Count > 0)
            {
                foreach( Product product in ItemsProduction.GetProduced() )
                {
                    if(product is Armor)
                    {
                        Storage.Products.Add(product);
                    }                        
                }
            }
            else
            {
                CatBag.Fleas += ItemsProduction.GetProducedResource();
            }

            FortUpdated.Invoke();            
        }
        else
            TimeTillProduction += Time.deltaTime;
    }

    [Range(0, 300)]
    public float ConsumptionInterval = 60f;
    public float TimeTillConsumption { get; private set; }
    void Consumption()
    {
        if (TimeTillConsumption > ConsumptionInterval)
        {
            TimeTillConsumption = 0f;

            Storage.FoodValue -= Cats.Count;
            Storage.WaterValue -= 2 * Cats.Count;

            FortUpdated.Invoke();            
        }
        else
            TimeTillConsumption += Time.deltaTime;
    }

    void CheckWarriorsAlive()
    {
        for (int i = 0; i < Warriors.Length; i++)
        {
            if (Warriors[i] != null)
            {
                if (Warriors[i].AI_Pawn.Dead == true)
                {
                    RemoveCat(Warriors[i]);
                }
            }
        }
    }

    [Range(0, 300)]
    public float SpawnInterval = 60f;
    float TimeTillSpawn = 0f;
    void CheckSpawn()
    {
        if (TimeTillSpawn > SpawnInterval)
            SpawnCat();
        else
            TimeTillSpawn += Time.deltaTime;
    }

    void SpawnCat()
    {
        if(Happiness >= 70)
        {
            if (NumberOfCats < MaxNumberOfCats)
            {
                AddCat();
            }
        }    
        else if(Happiness < 30)
        {
            if(Cats.Count > 0)
                RemoveCat(Cats[Cats.Count - 1]);
        }
    }

    void AddCat()
    {
        Cat newCat = new Cat() { Level = new Level(10, new Action[10]), Type = Cat.EType.Worker };
        Cats.Add(newCat);
        newCat.AI_Pawn = CreateAIPawn();
        NumberOfCats++;
        ItemsProduction.AddCat(newCat);
        TimeTillSpawn = 0f;

        FortUpdated.Invoke();
    }

    public void RemoveCat(Cat cat)
    {
        if(cat.Type == Cat.EType.Worker)
        {
            cat.Group.RemoveCat(cat);            
        }
        else
        {
            Destroy(cat.AI_Pawn.gameObject);
            cat.AI_Pawn = null;
            if (cat.Armor != null)
            {
                Storage.Products.Add(cat.Armor);
                cat.Armor = null;
            }
            for (int i = 0; i < Warriors.Length; i++)
            {
                if (Warriors[i] == cat)
                {
                    Warriors[i] = null;
                    break;
                }
            }
        }

        Cats.Remove(cat);
        NumberOfCats--;
        TimeTillSpawn = 0f;

        FortUpdated.Invoke();
    }

    public void MakeWarrior(Cat cat, int index)
    {
        if (cat.Type != Cat.EType.Warrior)
        {
            if (index >= 0 && index < MaxNumberOfWarriors)
            {
                if(Warriors[index] != null)
                    MakeWorker(Warriors[index], ItemsProduction);

                Warriors[index] = cat;
                cat.Type = Cat.EType.Warrior;
                //cat.AI_Pawn = CreateAIPawn();

                Squads[0].AddCat(cat);

                FortUpdated.Invoke();
            }
        }
    }

    public void MakeWorker(Cat cat, ResourceProduction production)
    {
        if (cat.Type != Cat.EType.Worker)
        {
            cat.Type = Cat.EType.Worker;
            if(cat.Armor != null)
            {
                Storage.Products.Add(cat.Armor);
                cat.Armor = null;
            }
            cat.Group.RemoveCat(cat);

            for (int i = 0; i < Warriors.Length; i++)
            {
                if(Warriors[i] == cat)
                {
                    Warriors[i] = null;
                    break;
                }
            }
            
            production.AddCat(cat);
            FortUpdated.Invoke();
        }
    }

    AI_Pawn CreateAIPawn(int creatureType = 0) //CreatureType - Ближний бой или дальний бой
    {
        RaceFlag myRaceFlag = LevelData.getMyRaceFlag(Pawn.ECreatureType.Cat);
        GameObject newCreature = null;

        if (myRaceFlag != null)
        {
            if (myRaceFlag.fortCreaturePrefab.Count > 0)
            {
                GameObject whatACreature = null;
                if (creatureType == 0)
                {
                    whatACreature = myRaceFlag.fortCreaturePrefab[UnityEngine.Random.Range(0, myRaceFlag.fortCreaturePrefab.Count)];
                }
                else if (creatureType == 1)
                {
                    whatACreature = myRaceFlag.fortCreaturePrefabRange[UnityEngine.Random.Range(0, myRaceFlag.fortCreaturePrefabRange.Count)];
                }
                
                CreaturePrefab = newCreature = (Instantiate(whatACreature, SpawnPosition, Quaternion.identity) as GameObject);
            }
        }

        if (newCreature == null) newCreature = (Instantiate(CreaturePrefab, SpawnPosition, Quaternion.identity) as GameObject);

        AI_Pawn ai_creature = newCreature.GetComponent<AI_Pawn>();

        newCreature.name = CreaturePrefab.name;

        return ai_creature;
    }

    Vector3 SpawnPosition
    {
        get
        {
            Vector3 spawnPosition = UnityEngine.Random.insideUnitSphere * 2f;
            spawnPosition.y = WarriorsSpawnPoint.position.y;

            spawnPosition = WarriorsSpawnPoint.position + spawnPosition.normalized * SpawnRange * UnityEngine.Random.value;
            return spawnPosition;
        }
    }
}

namespace Fort
{
    [Serializable]
    public class Cat
    {
        public enum EType { Worker, Warrior, Mage };
        public Level Level;
        public UnityEvent LevelUp;
        public EType Type = EType.Worker;

        [NonSerialized] public Group Group;

        [NonSerialized] public AI_Pawn AI_Pawn = null;
        [NonSerialized] public Armor Armor;
    }

    [Serializable]
    public class Level
    {
        public int Value;// { get; private set; }
        public int MaxValue;
        public Action[] LevelUpActions;

        public Level(int maxLevel, Action[] levelActions)
        {
            Value = 0;
            MaxValue = maxLevel;
            LevelUpActions = new Action[MaxValue];

            for (int i = 0; i < MaxValue; i++)
            {
                if (levelActions[i] != null)
                    LevelUpActions[i] = levelActions[i];
            }
        }

        public UnityEvent OnLevelUp = new UnityEvent();
        public virtual bool LevelUp()
        {
            if (Value < MaxValue)
            {
                Value++;
                LevelUpActions[Value - 1].Invoke();
                OnLevelUp.Invoke();
                return true;
            }
            else return false;
        }
    }
    
    public class Storage
    {
        [Range(0, 1000000)]
        public int FoodValue = 0;
        [Range(0, 1000000)]
        public int WaterValue = 0;
        public List<Product> Products = new List<Product>();
    }

    public abstract class Group
    {
        public string Name;
        public List<Cat> Cats = new List<Cat>();

        public virtual bool AddCat(Cat cat)
        {
            if (cat.Group != this)
            {
                if (cat.Group != null)
                    cat.Group.RemoveCat(cat);

                cat.Group = this;

                return true;
            }
            return false;
        }

        public virtual bool RemoveCat(Cat cat)
        {
            if (cat.Group == this) cat.Group = null;
            return Cats.Remove(cat);
        }
    }

    public class ResourceProduction : Group
    {
        public List<Cat> NextCats = new List<Cat>();
        public float ResourceSpeed;
        public int DefaultSpeed = 0;

        public override bool AddCat(Cat cat)
        {
            if (base.AddCat(cat))
            {
                if (!NextCats.Contains(cat)) NextCats.Add(cat);

                return true;
            }
            return false;
        }

        public override bool RemoveCat(Cat cat)
        {
            if (!base.RemoveCat(cat))
                return NextCats.Remove(cat);
            else return true;
        }

        protected void ReplaceCatsFromReserve()
        {
            Cats.AddRange(NextCats);
            NextCats.RemoveRange(0, NextCats.Count);
        }

        public int ProducedResource { get { return (int)ResourceSpeed * Cats.Count + DefaultSpeed; } }

        public int GetProducedResource()
        {
            int produced = ProducedResource;
            ReplaceCatsFromReserve();
            return produced;
        }
    }

    public class ItemsProduction : ResourceProduction
    {
        public List<Product> AvailableProducts = new List<Product>();
        public List<Product> ProductionQueue = new List<Product>();
        //public float MolotochkiSpeed;
        int Molotochki = 0;

        public List<Product> GetProduced()
        {
            Molotochki += (int)ResourceSpeed * Cats.Count + DefaultSpeed;
            List<Product> returnList = new List<Product>();

            ReplaceCatsFromReserve();

            while (Molotochki > 0 && ProductionQueue.Count > 0)
            {
                Molotochki = ProductionQueue[0].AddProgress(Molotochki);

                if (ProductionQueue[0].GetProgress >= ProductionQueue[0].RequiredMolotochki)
                {
                    Product product = ProductionQueue[0];

                    if (product is Building)
                    {
                        Building building = product as Building;

                        if (building.Built)
                        {
                            (product as Building).Upgrade();
                            if ((product as Building).Level.Value >= (product as Building).Level.MaxValue)
                                ProductionQueue.RemoveAt(0);
                        }
                        else
                        {
                            building.Build();
                        }
                    }
                    else if (product is Armor)
                    {
                        ProductionQueue.RemoveAt(0);
                    }

                    returnList.Add(product);
                }
            }

            return returnList;
        }

        public void Replace(Product productReplaced, Product product)
        {
            if (ProductionQueue.Contains(product) && ProductionQueue.Contains(productReplaced))
            {
                OurFort.MoveInList(ProductionQueue, product, productReplaced);
            }
        }
    }

    public class BattleSquad : Group
    {
        public CreatureSpawn AttackPoint;
        public List<AI_Pawn> BattleGroup = new List<AI_Pawn>();
        public Vector3 FortPosition;

        public override bool AddCat(Cat cat)
        {
            if(base.AddCat(cat))
            {
                if (!Cats.Contains(cat)) Cats.Add(cat);
                if (!BattleGroup.Contains(cat.AI_Pawn)) BattleGroup.Add(cat.AI_Pawn);
                return true;
            }
            return false;
        }

        public override bool RemoveCat(Cat cat)
        {
            if (base.RemoveCat(cat))
                return BattleGroup.Remove(cat.AI_Pawn);
            else return false;
        }

        public UnityEvent OnAttack;
        public void Atac()
        {
            foreach (var cat in BattleGroup)
            {
                cat.simulateFlock = true;
                cat.isBattleFlock = true;

                cat.flockLeader = BattleGroup[0];
                cat.flockPawns = BattleGroup;

                cat.Plans_SetEnemySpawnPoint(AttackPoint);
            }
        }

        public UnityEvent OnProtect;
        public void Protec()
        {
            foreach (var cat in BattleGroup)
            {
                cat.flockLeader = null;
                cat.simulateFlock = false;
                cat.isBattleFlock = false;
                cat.AiPlans = AI_Pawn.EAiPlans.Null;
                cat.Ai_SetWaypoint(FortPosition, true);
            }
        }
    }
}