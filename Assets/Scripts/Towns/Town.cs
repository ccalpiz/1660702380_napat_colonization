using UnityEngine;
using UnityEngine.EventSystems;

public class Town : MonoBehaviour, IPointerClickHandler
{
    [SerializeField]
    protected Faction faction;
    public Faction Faction { get { return faction; } }

    [SerializeField]
    protected int visualRange;
    public int VisualRange { get { return visualRange; } }

    [SerializeField]
    private Vector2 curPos;
    public Vector2 CurPos { get { return curPos; } set { curPos = value; } }

    [SerializeField]
    protected Hex curHex;
    public Hex CurHex { get { return curHex; } set { curHex = value; } }

    [Header("Town")]
    [SerializeField]
    protected SpriteRenderer townSprite;
    public SpriteRenderer TownSprite { get { return townSprite; } }

    [Header("Border")]
    [SerializeField]
    protected SpriteRenderer borderSprite;

    [Header("Flag")]
    [SerializeField]
    protected SpriteRenderer flagSprite;

    [SerializeField]
    private int[] warehouse = new int[16];
    public int[] Warehouse { get { return warehouse; } set { warehouse = value; } }

    [SerializeField]
    private int[] totalYieldThisTurn = new int[16]; //no. of all resource production this turn
    public int[] TotalYieldThisTurn { get { return totalYieldThisTurn; } set { totalYieldThisTurn = value; } }

    [SerializeField]
    private int crossNum; //no. of crosses produced in this turn
    public int CrossNum { get { return crossNum; } set { crossNum = value; } }

    [SerializeField]
    private int bellNum; //no. of bells produced in this turn
    public int BellNum { get { return bellNum; } set { bellNum = value; } }

    [SerializeField]
    protected GameManager gameManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TownInit(GameManager gameMgr, Faction fact)
    {
        gameManager = gameMgr;
        faction = fact;
        flagSprite.sprite = fact.FlagIcon;
        townSprite.sprite = fact.TownIcon;
    }

    public void AccumulateToWarehouse()
    {
        for (int i = 0; i < 16; i++)
        {
            warehouse[i] += totalYieldThisTurn[i];
        }
    }

    //Workshops: a worker turns 3 raw goods into 3 finished goods per turn, an expert 6 (same as the Week 9 charts)
    //0 Rum Distiller, 1 Tobacconist, 2 Weaver, 3 Fur Trader, 4 Blacksmith, 5 Armory (runs after the Blacksmith, so new tools count)
    public static readonly int[] WORKSHOP_IN = { 1, 2, 3, 4, 6, 14 };    //Sugar, Tobacco, Cotton, Furs, Ore, Tools
    public static readonly int[] WORKSHOP_OUT = { 9, 10, 11, 12, 14, 15 }; //Rum, Cigar, Cloth, Coat, Tools, Muskets
    public static readonly LandUnitType[] WORKSHOP_EXPERT = { LandUnitType.RumDistillers, LandUnitType.Tobacconists,
        LandUnitType.CottonWeavers, LandUnitType.FurTraders, LandUnitType.Blacksmiths, LandUnitType.Gunsmiths };
    public const int WORKSHOP_BASE = 3;
    public const int MAX_WORKSHOP_WORKERS = 3;

    public System.Collections.Generic.List<LandUnit> WorkshopWorkers(int w)
    {
        var list = new System.Collections.Generic.List<LandUnit>();
        foreach (Unit unit in curHex.UnitsInHex)
            if (unit is LandUnit land && land.UnitStatus == UnitStatus.WorkInField && land.Workshop == w)
                list.Add(land);
        return list;
    }

    public int WorkshopCapacity(int w)
    {
        int n = 0;
        foreach (LandUnit worker in WorkshopWorkers(w))
            n += worker.LandUnitType == WORKSHOP_EXPERT[w] ? WORKSHOP_BASE * 2 : WORKSHOP_BASE;
        return n;
    }

    //apply = true: end of turn, after AccumulateToWarehouse. apply = false: preview for the town panel (stock + this turn's yield).
    //Each workshop makes as much as the stored raw goods allow.
    public int[] RunWorkshops(bool apply)
    {
        int[] stock = apply ? warehouse : (int[])warehouse.Clone();
        if (!apply)
            for (int i = 0; i < stock.Length; i++)
                stock[i] += totalYieldThisTurn[i];

        int[] made = new int[WORKSHOP_IN.Length];
        for (int w = 0; w < made.Length; w++)
        {
            made[w] = Mathf.Min(WorkshopCapacity(w), Mathf.Max(0, stock[WORKSHOP_IN[w]]));
            stock[WORKSHOP_IN[w]] -= made[w];
            stock[WORKSHOP_OUT[w]] += made[w];
        }
        return made;
    }

    public const int FOOD_PER_COLONIST = 2;
    public const int FOOD_TO_GROW = 200;

    public int ColonistCount()
    {
        int n = 0;
        foreach (Unit unit in curHex.UnitsInHex)
            if (unit.UnitType == UnitType.Land && unit.UnitStatus == UnitStatus.WorkInField)
                n++;
        return n;
    }

    public int NetFoodPerTurn() { return totalYieldThisTurn[0] - ColonistCount() * FOOD_PER_COLONIST; }

    //Runs after AccumulateToWarehouse (food already added). Returns a message for the player, or null.
    public string ConsumeFood()
    {
        warehouse[0] -= ColonistCount() * FOOD_PER_COLONIST;

        if (warehouse[0] >= FOOD_TO_GROW)
        {
            warehouse[0] -= FOOD_TO_GROW;
            gameManager.SpawnFreeColonist(this);
            return "Our colony has plenty of food. A new <color=yellow>Free Colonist</color> has joined the settlement.";
        }

        if (warehouse[0] < 0)
        {
            warehouse[0] = 0;
            string dead = StarveOneColonist();
            if (dead != null)
                return $"Famine! A <color=yellow>{dead}</color> has starved to death. Put more colonists on food tiles.";
        }
        else if (warehouse[0] + NetFoodPerTurn() < 0)
            return $"Food is running out. Our colony needs <color=yellow>{-NetFoodPerTurn()}</color> more food each turn, or a colonist will starve next turn.";

        return null;
    }

    private string StarveOneColonist()
    {
        foreach (Hex hex in HexCalculator.GetHexAroundToArray(gameManager.AllHexes, curHex))
        {
            if (hex == null || hex.Labor == null || hex.Labor.Faction != faction)
                continue;

            Unit worker = hex.Labor;
            if (hex.YieldID != -1) //remove this worker's output, same formula as TerrainSlot.ConvertNormalToActualYield
            {
                int amount = Mathf.Max(0, hex.ResourceYield[hex.YieldID]);
                if (gameManager.GetIfProductIDDoubledFromExpert(hex) == hex.YieldID)
                    amount *= 2;
                totalYieldThisTurn[hex.YieldID] = Mathf.Max(0, totalYieldThisTurn[hex.YieldID] - amount);
            }
            hex.Labor = null;
            hex.YieldID = -1;

            string name = worker.UnitName;
            gameManager.SetupDeadUnit(worker);
            return name;
        }

        for (int w = 0; w < WORKSHOP_IN.Length; w++) //no field workers left: a workshop worker starves
        {
            var workers = WorkshopWorkers(w);
            if (workers.Count == 0)
                continue;
            string name = workers[0].UnitName;
            workers[0].Workshop = -1;
            gameManager.SetupDeadUnit(workers[0]);
            return name;
        }
        return null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        //Debug.Log("Mouse Click");
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (faction == gameManager.PlayerFaction)
                gameManager.SetupCurrentTown(this);
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            //Debug.Log($"To Move Hex:{curHex.X}, {curHex.Y}");
            if (gameManager.CurUnit == null || gameManager.CurUnit.CurHex == null) //selected ship is away in Europe
                return;
            if (gameManager.CheckIfHexIsAdjacent(gameManager.CurUnit.CurHex, curHex))
            {
                if (faction == gameManager.PlayerFaction)//same side unit
                {
                    //Debug.Log("True");
                    gameManager.CurUnit.PrepareMoveToHex(curHex);
                }
                else//diff side unit
                {
                    //Debug.Log($"{gameMgr.CurUnit} Attacks {townName}");
                }
            }
        }
    }
}
