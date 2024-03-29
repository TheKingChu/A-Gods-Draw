// Written by
//  Javier Villegas
// Modified by
//  Henrik Hustoft,
//  Nicolay Joahsen
//  Charlie Eik�s

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;


public class ActionCard_Behaviour : Card_Behaviour
{
    protected new ActionCard_ScriptableObject card_so;
    public Transform RootTransform;
    CardType cardType; 
    [SerializeField]
    EventReference SoundClick;
    BoardElement target;
    List<BoardElement> SelectedTargets = new List<BoardElement>();
    public BoardElement[] AllTargets => SelectedTargets.ToArray();
    Coroutine onSelectedRoutine;
    Coroutine actionRoutine;
    bool hasClickedTarget, cardIsReady, missedClick, IsOnBoard;
    public bool CardIsPlaced => IsOnBoard;
    public int neededLanes = 1;
    ActionGroup _actionGroup {get => stats.actionGroup; set{stats.actionGroup = value;}}
    ActionGroup _godBuffActions {get => stats.godBuffActions; set{stats.godBuffActions = value;}}

    public CardType GetCardType => cardType;
    public ActionCard_ScriptableObject CardSO => card_so;
    Collider thisCollider;

    private void Update() {
        if(CheckForGod())
            elements.Glow.SetActive(true);
        else
            elements.Glow.SetActive(false);

        if(!CanBeSelected() && !IsOnBoard)
            thisCollider.enabled = false;
        else 
            thisCollider.enabled = true;
    }

    private IEnumerator SelectingTargets()
    {
        CheckForGod();

        CameraMovement.instance.SetCameraView(stats.TargetingView);
        
        for (int i = 0; i < stats.numberOfTargets; i++)
        {
            hasClickedTarget = false;
            missedClick = false;
            yield return new WaitUntil(() => hasClickedTarget);
            
            if(target as ActionCard_Behaviour == this)
            {
                MissClick();
                yield break;
            }

            if(!IsValidSelection(target, stats.SelectionType))
            {
                MissClick();
                yield break;    
            }
                
            SelectedTargets.Add(target);

            target = null;
        }
        
        CameraMovement.instance.SetCameraView(CameraView.Reset);
        cardIsReady = true;
        controller.GetBoard().PlayCard(this);
        TurnController.shouldWaitForAnims = false;
    }

    private bool IsValidSelection(BoardElement target, CardSelectionType selectionType)
    {
        string targetClassName = target.ClassName;

        // Debug.Log(targetClassName);

        if(targetClassName.Equals("None"))
            return false;

        if(targetClassName.Equals("BoardElement"))
            return true;

        int monsterIndex = BoardElementClassNames.instance.getIndexOf("Monster"); 
        int targetIndex = BoardElementClassNames.instance.getIndexOf(targetClassName);

        if(monsterIndex == selectionType.Index)
        {
            if(targetClassName.Contains("Monster"))
                return true;
        }

        int ActionCardIndex =  BoardElementClassNames.instance.getIndexOf("ActionCard_Behaviour");

        if(ActionCardIndex == selectionType.Index)
        {
            ActionCard_Behaviour card = target as ActionCard_Behaviour;

            if(card == null)
                return false;

            Debug.Log("this is a card: " + card.CardIsPlaced);

            if(card.CardIsPlaced)
                return true;
            return false;
        }

        if(targetIndex == selectionType.Index)
            return true;
        return false;
    }

    private bool AllActionsReady()
    {
        return cardIsReady && onPlayerHand;
    }
    public void MissClick()
    {
        missedClick = true;
        CancelSelection();
    }

    public void Initialize(ActionCard_ScriptableObject card, CardElements elements)
    {
        ClassName = "ActionCard_Behaviour";
        RootTransform = transform.parent;
        this.card_so = card;
        stats = card.cardStats.Clone();

        for (int i = 0; i < _actionGroup.actionStats.Count; i++)
        {
            CardAction act = GetAction(_actionGroup.actionStats[i].actionEnum);

            act.action_SFX = _actionGroup.actionStats[i].action_SFX;
            // act.PlayOnPlacedOrTriggered_SFX = _actionGroup.actionStats[i].PlayOnPlacedOrTriggered_SFX;
            act._VFX = _actionGroup.actionStats[i]._VFX;

            act.SetBehaviour(this);
            _actionGroup.Add(act); 
        }

        for (int i = 0; i < _godBuffActions.actionStats.Count; i++)
        {
            var act = GetAction(_godBuffActions.actionStats[i].actionEnum);
            
            act.action_SFX = _godBuffActions.actionStats[i].action_SFX;
            // act.PlayOnPlacedOrTriggered_SFX = _godBuffActions.actionStats[i].PlayOnPlacedOrTriggered_SFX;
            act.SetBehaviour(this);
            _godBuffActions.Add(act);
        }

        this.cardType = card.type;
        this.elements = elements;

        Material GlowMat = elements.Glow.GetComponentInChildren<Renderer>().material;
        GodColor color = GodColorGetter.find(card.cardStats.correspondingGod);
        GlowMat.SetColor("_MainColor", color.MainColor);
        GlowMat.SetColor("_SecondColor", color.SecondaryColor);

        thisCollider = GetComponent<Collider>();
    }
    public void Buff(int value, bool isMult)
    {
        if(!IsOnBoard)
            return;

        if(isMult)
            stats.strength *= value;
        else
            stats.strength += value;
        elements.strength.text = stats.formattedStrength;
        elements.Description.PopupInfo.Info = ActionCard_ScriptableObject.getEffectFormatted(this);

        if(CardAttackTotal(true).Item1)
            UpdateQueuedDamage(true);
    }

    public void DeBuff(int value, bool isDivided)
    {
        if(!IsOnBoard)
            return;

        if(isDivided)
            stats.strength /= value;
        else
            stats.strength -= value;
        elements.strength.text = stats.formattedStrength;

        if(CardAttackTotal(true).Item1)
            UpdateQueuedDamage(true);
    }

    public bool CheckForGod()
    {
        if(!controller)
            return false;

        if (!controller.GetBoard().playedGodCard)
            return false;

        if (stats.correspondingGod != controller.GetBoard().playedGodCard.CardSO.godAction)
            return false;

        // controller.GetBoard().playedGodCard.Buff(this);
        Debug.LogWarning("something funky might be here");
        return true;
    }

    public void EnemyDied(Monster enemy) => SelectedTargets.Remove(enemy);

    public void RemoveFromHand()
    {
        controller.Discard(this);
    }

    protected override void OnBeingSelected()
    {
        if (onSelectedRoutine == null)
        {
            if (!CanBeSelected())
            {
                CancelSelection();
                return;
            }
            TurnController.shouldWaitForAnims = true;
            onSelectedRoutine = StartCoroutine(SelectingTargets());

            //change cursor type depending on card type
            if(cardType == CardType.Attack)
                ChangeCursor.instance.AttackCursor();
            if(cardType == CardType.Defence)
                ChangeCursor.instance.DefenceCursor();
            if(cardType == CardType.Buff) 
                ChangeCursor.instance.BuffCursor();
            if(cardType == CardType.God)
                ChangeCursor.instance.GodCursor();

            //camera position by card type
            if(cardType == CardType.Attack || cardType == CardType.Defence)
            {
                CameraMovement.instance.SetCameraView(CameraView.EnemyCloseUp);
            }
            else if(cardType == CardType.Buff)
            {
                CameraMovement.instance.SetCameraView(CameraView.CardCloseUp);
            }
        }
    }

    protected override IEnumerator Play(BoardStateController board)
    {
        for (int i = 0; i < _actionGroup.actions.Count; i++)
        {
            CardAction action = _actionGroup.actions[i];

            StartCoroutine(action.OnAction(board, this));
            // if (action.PlayOnPlacedOrTriggered_SFX)
            // {
            //     SoundPlayer.PlaySound(action.action_SFX, gameObject);
            // }
            yield return new WaitUntil(() => action.Ready);
        }

        if(CheckForGod())
        {
            foreach (var action in _godBuffActions.actions)
            {
                StartCoroutine(action.OnAction(board, this));
                // if (action.PlayOnPlacedOrTriggered_SFX)
                // {
                //     SoundPlayer.PlaySound(action.action_SFX, gameObject);
                // }
                yield return new WaitUntil(() => action.Ready);
            }
        }

        yield return new WaitForSeconds(0.2f);

        SelectedTargets.Clear();
        
        foreach (var action in stats.actionGroup.actions)
            action.Reset(board, this);
        foreach (var action in stats.godBuffActions.actions)
            action.Reset(board, this);

        controller.RemoveCardFromBoard(this);
        Destroy(transform.parent.gameObject);
        TurnController.shouldWaitForAnims = false;
    }

    internal override void OnClickOnSelected()
    {
        base.OnClickOnSelected();
        BoardElement element = TurnController.PlayerClick();

        if (element)
        {
            target = element;
            hasClickedTarget = true;
        }
        else
        {
            MissClick();
            hasClickedTarget = false;
        }
    }

    public override bool ShouldCancelSelection()
    {
        return this == null || missedClick;
    }
    public override bool CanBeSelected()
    {
        return base.CanBeSelected() && (controller.GetBoard().thingsInLane.Count + neededLanes <= 4);
    }
    public override bool CardIsReady()
    {
        return AllActionsReady();
    }
    private bool IsMonsterTargeted(Monster _targetToCheck)
    {

        foreach (Monster _monster in AllTargets)
        {

            if(_targetToCheck == _monster)
                return true;
            
        }

        return false;

    }
    public (bool, int) CardAttackTotal(bool _buffUpdate)
    {

        int damageTotal = 0;
        bool dealsDamage = false;
        CardAction[] _actions = stats.actionGroup.actions.ToArray(), _godBuffActions = stats.godBuffActions.actions.ToArray();
        foreach (CardAction action in _actions)
        {

            if(action is AttackCardAction)
            {

                dealsDamage = true;
                damageTotal += stats.strength;

            }
            else if(action is EarthquakeCardAction)
            {

                dealsDamage = true;
                int _tempDmg = stats.strength;
                foreach(Monster _target in controller.GetBoard().getLivingEnemies())
                {

                    if(!IsMonsterTargeted(_target))
                        _target.UpdateQueuedDamage(this, _tempDmg, _buffUpdate);

                }
                damageTotal += _tempDmg;

            }
            else if(action is LeachCardAction)
            {

                dealsDamage = true;
                damageTotal += stats.strength;

            }
            else if(action is SplashDMGCardAction)
            {

                dealsDamage = true;
                int _tempDmg = (int)((stats.strength / 2f) + 1f);
                foreach(Monster _target in controller.GetBoard().getLivingEnemies())
                {

                    if(!IsMonsterTargeted(_target))
                        _target.UpdateQueuedDamage(this, _tempDmg, _buffUpdate);

                }
                damageTotal += _tempDmg;

            }
            
        }

        if(stats.correspondingGod == GodActionEnum.None || !CheckForGod())
            return (dealsDamage, damageTotal);

        foreach (CardAction action in _godBuffActions)
        {

            if(action is AttackCardAction)
            {

                dealsDamage = true;
                damageTotal += stats.strength;

            }
            else if(action is EarthquakeCardAction)
            {

                dealsDamage = true;
                int _tempDmg = stats.strength;
                foreach(Monster _target in controller.GetBoard().getLivingEnemies())
                {
                    if(!IsMonsterTargeted(_target))
                        _target.UpdateQueuedDamage(this, _tempDmg, _buffUpdate);

                }
                damageTotal += _tempDmg;

            }
            else if(action is LeachCardAction)
            {

                dealsDamage = true;
                damageTotal += stats.strength;

            }
            else if(action is SplashDMGCardAction)
            {

                dealsDamage = true;
                int _tempDmg = (int)((stats.strength / 2f) + 1f);
                foreach(Monster _target in controller.GetBoard().getLivingEnemies())
                {

                    if(!IsMonsterTargeted(_target))
                        _target.UpdateQueuedDamage(this, _tempDmg, _buffUpdate);

                }
                damageTotal += _tempDmg;

            }
            
        }

        return (dealsDamage, damageTotal);

    }

    public void UpdateQueuedDamage(bool _buffUpdate = false)
    {

        (bool, int) _damageInfo = CardAttackTotal(_buffUpdate);
        if(_damageInfo.Item1)
        {

            foreach (Monster _monster in AllTargets)
            {

                _monster.UpdateQueuedDamage(this, _damageInfo.Item2, _buffUpdate);
                
            }

        }

    }
    protected override void OnPlacedInLane()
    {
        base.OnPlacedInLane();
        UpdateQueuedDamage();
        foreach (var target in SelectedTargets)
        {
            foreach (CardAction action in _actionGroup.actions)
            {
                action.OnLanePlaced(controller.GetBoard(), this);
            }
            if (CheckForGod())
            {
                foreach (CardAction action in _godBuffActions.actions)
                {
                    action.OnLanePlaced(controller.GetBoard(), this);
                }
            }
        }

        

        controller.resetSelectedCard();
        IsOnBoard = true;
        missedClick = true;
    }
    public override void OnAction()
    {
        TurnController.shouldWaitForAnims = true;

        Debug.Log("Errors happen here");

        StartCoroutine(Play(controller.GetBoard()));
    }
    public override void CancelSelection()
    {
        base.CancelSelection();
        if (onSelectedRoutine != null)
            StopCoroutine(onSelectedRoutine);
        if (actionRoutine != null)
            StopCoroutine(actionRoutine);
        onSelectedRoutine = null;
        actionRoutine = null;
        target = null;

        StopAllCoroutines();

        SelectedTargets.Clear();
    }

    public void ApplyLevels(CardExperience CurrentLevel)
    {
        stats.UpgradePath.Experience = CurrentLevel;

        for (int i = 0; i < CurrentLevel.Level; i++)
        {
            if(stats.UpgradePath.Upgrades.Length-1 < i)
                return;

            CardUpgradeType upgradeType = stats.UpgradePath.Upgrades[i].UpgradeType;
            ModifiableCardValue modifiableCardValue = stats.UpgradePath.Upgrades[i].ValueSelection;

            switch(upgradeType)
            {
                case CardUpgradeType.AddGlyph:
                    AddNewGlyph(stats.UpgradePath.Upgrades[i].AddGlyph);
                    break;
                case CardUpgradeType.RemoveGlyph:
                    int n = stats.UpgradePath.Upgrades[i].RemoveGlyphIndex;
                    RemoveGlyph(stats.UpgradePath.Upgrades[i].RemovableGlyph[n]);
                    break;
                case CardUpgradeType.ModifyValue:
                    upgradeModifiableValue(modifiableCardValue, stats.UpgradePath.Upgrades[i].EditedValue);
                    break;
            }
        }
    }

    void RemoveGlyph(CardActionEnum Glyph)
    {
        for (int i = 0; i < _actionGroup.actionStats.Count; i++)
        {
            if(_actionGroup.actionStats[i].actionEnum == Glyph)
            {
                Debug.Log("removing: " + _actionGroup.actions[i].GetType() + "from: " + card_so.cardName);
                _actionGroup.actions.RemoveAt(i);
                _actionGroup.actionStats.RemoveAt(i);
            }
        }
    }

    void AddNewGlyph(CardActionEnum Glyph)
    {
        CardAction act = GetAction(Glyph);
        Debug.Log("adding: " + act.GetType() + " to: " + card_so.cardName);

        // act.action_SFX = _actionGroup.actionStats[i].action_SFX; // this should be read from a scriptable object for the target action
        // act.PlayOnPlacedOrTriggered_SFX = _actionGroup.actionStats[i].PlayOnPlacedOrTriggered_SFX;
        // act._VFX = _actionGroup.actionStats[i]._VFX;

        act.SetBehaviour(this);
        _actionGroup.Add(act); 
        CardActionData _newAction = new CardActionData();
        _newAction.actionEnum = Glyph;
        _actionGroup.actionStats.Add(_newAction);
    }

    void upgradeModifiableValue(ModifiableCardValue Modify, int Value)
    {
        Debug.Log("Modifying: " + Modify + " to value: " + Value + " of " + card_so.cardName);

        switch(Modify)
        {
            case ModifiableCardValue.Strength:
                stats.strength = Value;
                elements.strength.text = stats.strength.ToString();
                break;
            
            case ModifiableCardValue.NumberOfTargets:
                stats.numberOfTargets = Value;
                break;

            case ModifiableCardValue.SelectionType:
                stats.SelectionType.Index = Value; 
                break;

            case ModifiableCardValue.CorrespondingGod:
                stats.correspondingGod = (GodActionEnum)Value;
                break;
        }
    }

    public override void GainExperience()
    {
        controller.addExperience(stats);
    }

    public override CardPlayData getCardPlayData()
    {
        CardPlayData data = new CardPlayData();
        data.CardType = card_so;
        data.Experience.XP = stats.UpgradePath.Experience.XP;
        data.Experience.Level = stats.UpgradePath.Experience.Level;
        data.Experience.ID = stats.UpgradePath.Experience.ID;
        return data;
    }
}