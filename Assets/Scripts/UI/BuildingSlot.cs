using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

//A workshop picture in the town panel. Drop up to 3 colonists here to turn raw goods into finished goods (rules in Town.cs).
public class BuildingSlot : MonoBehaviour, IDropHandler
{
    [SerializeField]
    private int workshop; //index into Town.WORKSHOP_IN/OUT
    public int Workshop { get { return workshop; } }

    [SerializeField]
    private Transform workerParent;
    public Transform WorkerParent { get { return workerParent; } }

    [SerializeField]
    private TMP_Text outputText;

    public void OnDrop(PointerEventData eventData)
    {
        UnitDrag unitDrag = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<UnitDrag>() : null;
        if (unitDrag == null || unitDrag.LandUnit.Workshop == workshop)
            return;

        Town town = GameManager.instance.CurTown;
        if (town.WorkshopWorkers(workshop).Count >= Town.MAX_WORKSHOP_WORKERS)
            return;

        unitDrag.QuitOldTerrainSlot(); //leaves its tile or other workshop
        unitDrag.IconParent = workerParent;
        unitDrag.LandUnit.UnitStatus = UnitStatus.WorkInField;
        unitDrag.LandUnit.Workshop = workshop;

        UIManager.instance.RefreshWorkshops();
        UIManager.instance.UpdateTotalFoodIcons(); //workshop workers eat too
    }

    public void UpdateText(int made)
    {
        Town town = GameManager.instance.CurTown;
        int cap = town.WorkshopCapacity(workshop);
        if (cap == 0)
        {
            outputText.text = "";
            return;
        }
        var products = GameManager.instance.ProductData;
        string outName = products[Town.WORKSHOP_OUT[workshop]].productName;
        string inName = products[Town.WORKSHOP_IN[workshop]].productName;
        outputText.text = made < cap
            ? $"+{made} {outName}\n<color=#E07050>needs {cap - made} more {inName}</color>"
            : $"+{made} {outName}";
    }
}
