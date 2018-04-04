using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using System;
using UnityEngine.Events;
using UI.Inventory;

public class KittenMenuController : MonoBehaviour
{
    public List<InnerButton> InnerButtons = new List<InnerButton>();
    public Color InnerButtonsColor = Color.cyan;
    public Color OuterButtonsColor = Color.cyan;

    InnerButton CurActiveInner = null;
    UIFoodSlot CurActiveOuter = null;

    Button CurrentInnerButton;

    [SerializeField]
    GameObject Center;
    [SerializeField]
    float InnerRadius = 90f;
    Vector2[] SlotsPositions = new Vector2[6];

    [SerializeField]
    float OuterRadius = 70f;
    Vector2[] OuterSlotsPositions = new Vector2[12];
    Vector2 InvisibelDummyPos = new Vector2();
    public GameObject InvisibelDummy;

    [SerializeField]
    GameObject FoodSlotsMenu;

    Vector2 StartPos;

    public GameObject FoodSlot;
    public List<UIFoodSlot> FoodSlots = new List<UIFoodSlot>();

    KittenInteraction KittenInteraction;
    KittenHealth KittenHealth;
    public InventoryBag InventoryBag;

    enum State { Inactive, Inner, Outer }
    State CurrentState = State.Inactive;

    public List<GameObject> BigBubbles = new List<GameObject>();
    public List<GameObject> SmallBubbles = new List<GameObject>();
    public Vector2 BubbleZeroPos = new Vector2();
    

    void Awake()
    {
        KittenHealth = KittenAI.GetInstance().KittenHealth;
        KittenInteraction = KittenAI.GetInstance().KittenInteraction;

        if (InventoryBag == null)
            InventoryBag = FindObjectOfType<InventoryBag>();

        StartPos = (Center.transform as RectTransform).anchoredPosition;

        for (int i = 0; i < 6; i++)
        {
            Vector2 newInnerPos = new Vector2();

            float innerAngle = (i - 1) * 60f * (Mathf.PI / 180f);

            newInnerPos.x = InnerRadius * Mathf.Sin(innerAngle);
            newInnerPos.y = InnerRadius * Mathf.Cos(innerAngle);
            newInnerPos += StartPos;

            SlotsPositions[i] = newInnerPos;
        }

        foreach (var curButton in InnerButtons)
        {
            (curButton.RectTransform as RectTransform).anchoredPosition = StartPos;
        }

        var feedButton = InnerButtons.Find(b => b.RectTransform.name == "Feed");
        feedButton.OnClick.AddListener(() => OpenFoodMenu());
    }

    float deltaMouse = 0f;
    public void Update()
    {
        foreach (var bubble in BigBubbles)
        {
            Vector3 pos = (bubble.transform as RectTransform).anchoredPosition;
            bubble.transform.Translate(0f, Time.deltaTime / 10f, 0f);

            if ((bubble.transform as RectTransform).anchoredPosition.y > 0f)
                (bubble.transform as RectTransform).anchoredPosition = BubbleZeroPos;
        }

        foreach (var bubble in SmallBubbles)
        {
            Vector3 pos = bubble.transform.position;
            bubble.transform.Translate(0f, Time.deltaTime / 5f, 0f);

            if ((bubble.transform as RectTransform).anchoredPosition.y > 0f)
                (bubble.transform as RectTransform).anchoredPosition = BubbleZeroPos;
        }

        UIManagerProgress.SetProgressBarPercentage("kittenFood", 100f - KittenHealth.hunger);
        UIManagerProgress.SetProgressBarPercentage("kittenWater", 100f - KittenHealth.waterThirst);
        UIManagerProgress.SetProgressBarPercentage("kittenDirt", 100f - KittenHealth.Dirt);
        UIManagerProgress.SetProgressBarPercentage("kittenPlay", 100f - KittenHealth.Fun);

        UIManagerProgress.SetProgressBarPercentage("kittenHealth", KittenHealth.fCurrentHealth);

        switch (CurrentState)
        {
            case State.Inner:

                if (Input.GetAxis("Mouse X") != 0 && Input.GetAxis("Mouse Y") != 0)
                {
                    Vector2 mousePos = UIManager.GetInstance().MouseToLocal(transform as RectTransform);
                    CurActiveInner = FindClosest(mousePos, InnerButtons);
                    RepaintInnerSlots();
                }

                if (Input.GetAxis("Mouse ScrollWheel") > 0f)
                {
                    CurActiveInner = InnerButtons[Mathf.Clamp(InnerButtons.IndexOf(CurActiveInner) + 1, 0, InnerButtons.Count - 1)];
                    RepaintInnerSlots();
                }
                else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
                {
                    CurActiveInner = InnerButtons[Mathf.Clamp(InnerButtons.IndexOf(CurActiveInner) - 1, 0, InnerButtons.Count - 1)];
                    RepaintInnerSlots();
                }

                if (Input.GetMouseButtonDown(0))
                {
                    if (CurActiveInner != null)
                        CurActiveInner.OnClick.Invoke();
                }

                if (Input.GetMouseButtonDown(1))
                {
                    iTween.Stop("MoveTo");
                    StopCoroutine("ShowButtons");

                    KittenInteraction.EndInteraction();
                    CurrentState = State.Inactive;
                }

                break;

            case State.Outer:

                deltaMouse += Input.GetAxis("Mouse Y");

                if (Input.GetMouseButtonDown(0))
                {
                    if (CurActiveOuter != null)
                        FoodPressed(CurActiveOuter);
                }

                if (Input.GetMouseButtonDown(1))
                {
                    iTween.Stop("MoveTo");
                    StopCoroutine("ShowButtons");

                    CloseFoodMenu();
                    CurrentState = State.Inner;
                    Cursor.visible = true;
                }

                if (Input.GetAxis("Mouse ScrollWheel") > 0f)
                    deltaMouse = 1f;
                else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
                    deltaMouse = -1f;

                CalculateAngleAlphas();
                OnOuterMouseMove();

                break;
        }
    }

    void RepaintInnerSlots()
    {
        foreach (var curButton in InnerButtons)
        {
            if (curButton == CurActiveInner)
                curButton.Icon.color = curButton.Glass.color = InnerButtonsColor;
            else
                curButton.Icon.color = curButton.Glass.color = Color.white;
        }
    }

    void OnOuterMouseMove()
    {
        if (deltaMouse >= 1f || deltaMouse <= -1f)
        {
            var prevActiveOuter = CurActiveOuter;

            if (deltaMouse >= 1f)
                CurActiveOuter = FoodSlots[Mathf.Clamp(FoodSlots.IndexOf(CurActiveOuter) + 1, 0, FoodSlots.Count - 1)];
            else if (deltaMouse <= -1f)
                CurActiveOuter = FoodSlots[Mathf.Clamp(FoodSlots.IndexOf(CurActiveOuter) - 1, 0, FoodSlots.Count - 1)];

            if (CurActiveOuter != prevActiveOuter)
            {
                RecalculateOuterAngles();
                MoveOuterSlots(.4f, iTween.EaseType.easeOutSine);
                RepaintOuterSlots();
            }
            deltaMouse = 0f;
        }
    }

    Vector2 ReplacePointByRotation(Vector2 point, float angle, Vector2 center)
    {
        var govno = point - center;
        govno = Quaternion.Euler(0, 0, angle) * govno;
        return center + govno;
    }

    void RecalculateOuterAngles()
    {
        if (FoodSlots.Count > 0 && CurActiveOuter != null)
        {
            int index = FoodSlots.IndexOf(CurActiveOuter);
            float initAngle = (CurActiveInner.OuterAngle - 30f * index) * (Mathf.PI / 180f);

            for (int i = 0; i < 12; i++)
            {
                Vector2 newOuterPos = new Vector2();

                newOuterPos.x = OuterRadius * Mathf.Sin(initAngle);
                newOuterPos.y = OuterRadius * Mathf.Cos(initAngle);
                //newOuterPos += CurActiveInner.RectTransform.anchoredPosition;

                OuterSlotsPositions[i] = newOuterPos;

                initAngle += 30f * (Mathf.PI / 180f);
            }
        }
    }

    void RepaintOuterSlots()
    {
        foreach (var curSlot in FoodSlots)
        {
            if (curSlot == CurActiveOuter)
            {
                curSlot.ShowValues();
                curSlot.ItemImage.color = OuterButtonsColor;
            }
            else
            {
                curSlot.HideValues();
                curSlot.ItemImage.color = Color.white;
            }
        }
    }

    InnerButton FindClosest(Vector2 mousePos, List<InnerButton> list)
    {
        float minDist = Vector2.Distance(list[0].RectTransform.anchoredPosition, mousePos);
        float dist;
        InnerButton theClosest = list[0];

        for (int i = 1; i < list.Count; i++)
        {
            dist = Vector2.Distance(list[i].RectTransform.anchoredPosition, mousePos);
            if (dist < minDist)
            {
                minDist = dist;
                theClosest = list[i];
            }
        }

        return theClosest;
    }

    private void OnEnable()
    {
        //Cursor.visible = false;
        CurrentState = State.Inner;
        StartCoroutine(ShowButtons());
    }

    private void OnDisable()
    {
        CurrentState = State.Inactive;
        foreach (var curButton in InnerButtons)
        {
            curButton.RectTransform.anchoredPosition = StartPos;
            curButton.RectTransform.gameObject.SetActive(false);
        }

        //CloseFoodMenu();
    }

    IEnumerator ShowButtons()
    {
        for (int i = 0; i < InnerButtons.Count; i++)
        {
            InnerButtons[i].RectTransform.gameObject.SetActive(true);
            //iTween.MoveTo(InnerButtons[i].RectTransform.gameObject, iTween.Hash( "x", SlotsPositions[i].x,
            //                                                                 "y", SlotsPositions[i].y,
            //                                                                 "time", .7f,
            //                                                                 "easetype", iTween.EaseType.easeOutElastic));

            UIManager.GetInstance().Move(SlotsPositions[i], InnerButtons[i].RectTransform, 0.7f, iTween.EaseType.easeOutElastic);

            yield return new WaitForSeconds(.1f);
        }
    }


    public void OpenFoodMenu()
    {
        var feedButton = InnerButtons.Find(b => b.RectTransform.name == "Feed");

        float curAngle = CurActiveInner.OuterAngle;
        Vector2 center = CurActiveInner.RectTransform.anchoredPosition;

        (InvisibelDummy.transform as RectTransform).anchoredPosition = OuterRadius * (new Vector2(Mathf.Sin(curAngle), Mathf.Cos(curAngle))) + center;

        UIManager.GetInstance().UIOpenAdditional("FoodSlots");

        int counter = 0;

        foreach (var curObj in InventoryBag.BagObjects)
        {
            if (curObj.InteractionObject.AdditionalInteraction is FoodInteraction)
            {
                counter++;
                if (counter > 12)
                    break;

                var newSlot = Instantiate(FoodSlot, FoodSlotsMenu.transform);
                (newSlot.transform as RectTransform).anchoredPosition = feedButton.RectTransform.anchoredPosition;

                var foodSlot = newSlot.GetComponent<UIFoodSlot>();
                foodSlot.FoodInteraction = curObj.InteractionObject.AdditionalInteraction as FoodInteraction;
                FoodSlots.Add(foodSlot);

                var image = newSlot.GetComponent<Image>();
                image.sprite = curObj.InventoryImageSprite;

                //var button = newSlot.GetComponent<Button>();
                //button.onClick.AddListener(() => FoodPressed(foodSlot));
            }
        }

        if (FoodSlots.Any())
        {
            CurActiveOuter = FoodSlots[0];
            StartCoroutine(ShowFoodButtons());
        }
    }

    IEnumerator ShowFoodButtons()
    {
        RecalculateOuterAngles();
        Cursor.visible = false;
        CurrentState = State.Inactive;

        for (int i = 0; i < FoodSlots.Count; i++)
        {
            FoodSlots[i].gameObject.SetActive(true);
            FoodSlots[i].OnAwake();
            FoodSlots[i].CanvasGroup.alpha = 0f;

            //iTween.MoveTo(FoodSlots[i].gameObject, iTween.Hash("x", OuterSlotsPositions[i].x,
            //                                                   "y", OuterSlotsPositions[i].y,
            //                                                   "time", .5f,
            //                                                   "easetype", iTween.EaseType.linear));

            UIManager.GetInstance().Move(OuterSlotsPositions[i], FoodSlots[i].RectTransform, 0.5f);
        }

        yield return new WaitForSeconds(.3f);

        RepaintOuterSlots();
        iTween.ValueTo(gameObject, iTween.Hash("from", 0f, "to", 1f, "time", .2f, "onupdate", "SetFoodSlotsAlpha"));
        yield return new WaitForSeconds(.2f);

        CurrentState = State.Outer;
    }

    public void SetFoodSlotsAlpha(float value)
    {
        for (int i = 0; i < FoodSlots.Count; i++)
        {
            float maxAlpha = CalculateAngleAlpha(FoodSlots[i].transform.position);
            if (value > maxAlpha)
                value = maxAlpha;

            FoodSlots[i].CanvasGroup.alpha = value;
        }
    }

    void CalculateAngleAlphas()
    {
        for (int i = 0; i < FoodSlots.Count; i++)
        {
            FoodSlots[i].CanvasGroup.alpha = CalculateAngleAlpha((FoodSlots[i].transform as RectTransform).anchoredPosition);
        }
    }

    float CalculateAngleAlpha(Vector2 point)
    {
        float innerAngleRad = CurActiveInner.OuterAngle * (Mathf.PI / 180f);
        Vector2 curInnerAngleVector = 10f * (new Vector2(Mathf.Sin(innerAngleRad), Mathf.Cos(innerAngleRad)));

        float angle = Vector2.Angle(point/* - CurActiveInner.RectTransform.anchoredPosition*/, curInnerAngleVector);

        float alpha = 0f;

        if (angle < 30f)
            alpha = 1f;
        else if (angle < 60f)
            alpha = (60f - angle) / 30f;
        else
            alpha = 0f;

        return alpha;
    }

    public void FoodPressed(UIFoodSlot foodSlot)
    {
        if (KittenInteraction.Feed(foodSlot.FoodInteraction))
        {
            int index = FoodSlots.IndexOf(foodSlot);

            if (FoodSlots.Count > 1)
            {
                if (index == FoodSlots.Count - 1)
                    CurActiveOuter = FoodSlots[index - 1];
                else
                    CurActiveOuter = FoodSlots[index + 1];
            }


            FoodSlots.Remove(foodSlot);
            Destroy(foodSlot.gameObject);

            if (FoodSlots.Any())
            {
                RecalculateOuterAngles();

                MoveOuterSlots(.4f, iTween.EaseType.easeOutElastic);
                RepaintOuterSlots();
            }
            else
            {
                CloseFoodMenu();
            }
        }
    }

    void MoveOuterSlots(float speed, iTween.EaseType easeType)
    {
        for (int i = 0; i < FoodSlots.Count; i++)
        {
            //iTween.MoveTo(FoodSlots[i].gameObject, iTween.Hash("x", OuterSlotsPositions[i].x, "y", OuterSlotsPositions[i].y, "time", speed, "easetype", easeType));

            UIManager.GetInstance().Move(OuterSlotsPositions[i], FoodSlots[i].RectTransform, speed, easeType);
        }
    }

    void CloseFoodMenu()
    {
        UIManager.GetInstance().UICloseAdditional("FoodSlots");

        for (int i = FoodSlots.Count - 1; i >= 0; i--)
        {
            Destroy(FoodSlots[i].gameObject);
            FoodSlots.RemoveAt(i);
        }

        CurrentState = State.Inner;
        Cursor.visible = true;
    }
}

[Serializable]
public class InnerButton
{
    public Image Icon;
    public Image Glass;
    public RectTransform RectTransform;
    public UnityEvent OnClick;
    public float OuterAngle;
}
