//modified by Charlie

using Map;
using System.Collections.Generic;
using UnityEngine;
using HH.MultiSceneTools;
using static UnityEditor.Progress;
using System.Collections;

public enum CardType
{
    Attack,
    Defence,
    Buff,
    Utility,
    God,
    None
}
public class ChooseCardReward : MonoBehaviour
{
    [SerializeField] CardReaderController CardInspector;
    List<Card_SO> searchResult = new();

    public Transform[] spots;
    Card_SO[] CardOptions;
    public GameObject prefab;


    [SerializeField]
    LayerMask laneLayer;

    //card confirmation
    [SerializeField] CardRewardOption[] rewardOptions;
    public bool shouldConfirmSelection;
    bool confirmed;
    [SerializeField] Transform EndOfPath, EndPosition;


    private void Start()
    {
        CardOptions = new Card_SO[spots.Length];
        CameraMovement.instance.SetCameraView(CameraView.CardReward);
        
        GettingType(GameManager.instance.nextRewardType);

    }

    bool hasClicked = false;
    private void Update()
    {
        if (!confirmed)
            checkSelected();

        if (CardInspector.isInspecting)
        {
            if (Input.GetMouseButtonDown(1))
            {
                CardInspector.returnInspection();
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (!CardInspector.isInspecting)
                {
                    CardInspector.returnInspection();
                    return;
                }

            }
            else
                hasClicked = false;
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public void GettingType(NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.Reward:
                searchResult = CardSearch.Search<Card_SO>();
                break;
            case NodeType.AttackReward:
                searchResult = CardSearch.Search<ActionCard_ScriptableObject>(new string[] { CardType.Attack.ToString() });
                break;
            case NodeType.DefenceReward:
                searchResult = CardSearch.Search<ActionCard_ScriptableObject>(new string[] { CardType.Defence.ToString() });
                break;
            case NodeType.BuffReward:
                searchResult = CardSearch.Search<ActionCard_ScriptableObject>(new string[] { CardType.Buff.ToString() });
                break;
            case NodeType.GodReward:
                searchResult = CardSearch.Search<ActionCard_ScriptableObject>(new string[] { "Tyr" });
                break;
                
        }
        InstantiateCards();
    }

    void InstantiateCards()
    {
        for (int i = 0; i < spots.Length; i++)
        {
            if (searchResult.Count <= 0)
            {
                break;
            }

            GameObject spawn = Instantiate(prefab, spots[i]);

            int randomIndex = Random.Range(0, searchResult.Count);
            Card_SO randomCard = searchResult[randomIndex];
            Card_Loader _Loader = spawn.GetComponentInChildren<Card_Loader>();
            _Loader.addComponentAutomatically = false;
            
            CardPlayData _randomData = new CardPlayData();
            _randomData.CardType = randomCard;
            _Loader.Set(_randomData);

            CardOptions[i] = searchResult[randomIndex];
            rewardOptions[i].AddToDeck = searchResult[randomIndex];
            searchResult.RemoveAt(randomIndex);
        }
    }

    void checkSelected()
    {
        for (int i = 0; i < rewardOptions.Length; i++)
        {
            if (rewardOptions[i].isBeingInspected && shouldConfirmSelection)
            {
                confirmDeck(rewardOptions[i]);
                break;
            }
        }
        shouldConfirmSelection = false;
    }

    void confirmDeck(CardRewardOption Selected)
    {
        confirmed = true;
        //GameManager.instance.PlayerTracker.setDeck(Selected.StarterDeck.deckData);
        DeckList_SO.playerObtainCard(Selected.AddToDeck);
        GameSaver.SaveData(GameManager.instance.PlayerTracker.CurrentDeck.deckData.GetDeckData());
        Map.Map_Manager.SavingMap();
        StartCoroutine(animateDeck(Selected));
    }

    IEnumerator animateDeck(CardRewardOption selected)
    {
        EndOfPath.position = EndPosition.position;
        yield return new WaitUntil(() => !selected.isBeingInspected);
        GameManager.instance.shouldGenerateNewMap = true;
        yield return new WaitForSeconds(0.3f);
        MultiSceneLoader.loadCollection("Map", collectionLoadMode.Difference);
    }

    int SelectReward()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100, laneLayer))
        {
            for (int i = 0; i < spots.Length; i++)
            {
                Transform target = hit.collider.transform.parent;
                if (target.Equals(spots[i]))
                {
                    return i;
                }
            }
        }
        return -1;
    }
}
